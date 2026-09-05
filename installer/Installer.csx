using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

[assembly: AssemblyTitle("SolidWorks to Bambu Studio Setup")]
[assembly: AssemblyDescription("Installer for the SOLIDWORKS to Bambu Studio add-in")]
[assembly: AssemblyCompany("SolidWorksToBambu")]
[assembly: AssemblyProduct("SolidWorksToBambu")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

namespace SolidWorksToBambu.Setup
{
    internal static class Installer
    {
        private const string ProductName = "SOLIDWORKS -> Bambu Studio";
        private const string ProductVersion = "0.2.0";
        private const string ResourcePrefix = "SolidWorksToBambu.Installer.Resources.";
        private const string UninstallRegistryPath =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SolidWorksToBambu";

        private static readonly string[] PayloadFiles =
        {
            "SolidWorksToBambu.dll",
            "SolidWorksToBambu.pdb",
            "SolidWorksTools.dll",
            "Repair-SystemRegistration.ps1",
            "Uninstall.ps1"
        };

        [STAThread]
        private static int Main(string[] args)
        {
            bool verifyOnly = args.Length == 1 &&
                string.Equals(args[0], "--verify", StringComparison.OrdinalIgnoreCase);

            try
            {
                ValidatePayload();
                if (verifyOnly)
                {
                    return 0;
                }

                if (!IsAdministrator())
                {
                    return RelaunchElevated();
                }

                if (Process.GetProcessesByName("SLDWORKS").Length > 0)
                {
                    MessageBox.Show(
                        "请先保存文件并关闭 SOLIDWORKS，然后重新运行安装程序。",
                        ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 2;
                }

                string documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string installDirectory = Path.Combine(documentsDirectory, "SolidWorksToBambu", "addin");
                Directory.CreateDirectory(installDirectory);

                foreach (string payloadFile in PayloadFiles)
                {
                    ExtractPayload(payloadFile, Path.Combine(installDirectory, payloadFile));
                }

                string installedDll = Path.Combine(installDirectory, "SolidWorksToBambu.dll");
                string repairScript = Path.Combine(installDirectory, "Repair-SystemRegistration.ps1");
                RunRegistrationRepair(repairScript, installedDll);
                RegisterUninstaller(installDirectory);

                MessageBox.Show(
                    "安装完成。\r\n\r\n现在可以启动 SOLIDWORKS 2025，加载项将自动启用。",
                    ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    return 1223;
                }

                if (!verifyOnly)
                {
                    ShowFailure(ex);
                }
                return 1;
            }
            catch (Exception ex)
            {
                if (!verifyOnly)
                {
                    ShowFailure(ex);
                }
                return 1;
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static int RelaunchElevated()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static void ValidatePayload()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string payloadFile in PayloadFiles)
            {
                string resourceName = ResourcePrefix + payloadFile;
                using (Stream resource = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resource == null || resource.Length == 0)
                    {
                        throw new InvalidDataException("安装包缺少文件：" + payloadFile);
                    }
                }
            }
        }

        private static void ExtractPayload(string payloadFile, string destinationPath)
        {
            string resourceName = ResourcePrefix + payloadFile;
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (source == null)
                {
                    throw new InvalidDataException("无法读取安装文件：" + payloadFile);
                }

                string temporaryPath = destinationPath + ".installing";
                try
                {
                    using (FileStream destination = new FileStream(
                        temporaryPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        source.CopyTo(destination);
                    }

                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    File.Move(temporaryPath, destinationPath);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }

        private static void RunRegistrationRepair(string repairScript, string installedDll)
        {
            string powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = powershell,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(repairScript) +
                    " -InstalledDll " + Quote(installedDll),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "SOLIDWORKS 加载项注册失败，错误代码：" + process.ExitCode);
                }
            }
        }

        private static void RegisterUninstaller(string installDirectory)
        {
            string uninstallScript = Path.Combine(installDirectory, "Uninstall.ps1");
            string powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            string uninstallCommand = Quote(powershell) +
                " -NoProfile -ExecutionPolicy Bypass -File " + Quote(uninstallScript);

            using (RegistryKey machineRoot = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                RegistryView.Registry64))
            using (RegistryKey key = machineRoot.CreateSubKey(UninstallRegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法创建 Windows 卸载信息。");
                }

                key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", ProductVersion, RegistryValueKind.String);
                key.SetValue("Publisher", "SolidWorksToBambu", RegistryValueKind.String);
                key.SetValue("InstallLocation", installDirectory, RegistryValueKind.String);
                key.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 256, RegistryValueKind.DWord);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture), RegistryValueKind.String);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void ShowFailure(Exception exception)
        {
            MessageBox.Show(
                "安装失败：" + exception.Message,
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
