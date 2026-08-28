using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SystemEnvironment = System.Environment;

namespace SolidWorksToBambu
{
    internal static class Logger
    {
        private static readonly object Sync = new object();

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                string root = Path.Combine(
                    SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.LocalApplicationData),
                    "SolidWorksToBambu");
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, "SolidWorksToBambu.log");
                StringBuilder line = new StringBuilder();
                line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                line.Append(" [").Append(level).Append("] ").Append(message);
                if (exception != null)
                {
                    line.AppendLine();
                    line.Append(exception);
                }

                lock (Sync)
                {
                    File.AppendAllText(path, line + SystemEnvironment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never interrupt SOLIDWORKS.
            }
        }
    }

    internal sealed class PluginSettings
    {
        private const string RegistryPath = @"Software\SolidWorksToBambu";

        public string BambuStudioPath { get; set; }
        public bool KeepExportFiles { get; set; }
        public int CleanupDays { get; set; }

        public static PluginSettings Load()
        {
            PluginSettings settings = new PluginSettings
            {
                BambuStudioPath = string.Empty,
                KeepExportFiles = false,
                CleanupDays = 7
            };

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null)
                    {
                        return settings;
                    }

                    settings.BambuStudioPath = Convert.ToString(key.GetValue("BambuStudioPath", string.Empty));
                    settings.KeepExportFiles = Convert.ToInt32(key.GetValue("KeepExportFiles", 0)) != 0;
                    settings.CleanupDays = Math.Max(1, Math.Min(30, Convert.ToInt32(key.GetValue("CleanupDays", 7))));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("读取设置失败", ex);
            }

            return settings;
        }

        public void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法创建插件设置注册表项。");
                }

                key.SetValue("BambuStudioPath", BambuStudioPath ?? string.Empty, RegistryValueKind.String);
                key.SetValue("KeepExportFiles", KeepExportFiles ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("CleanupDays", Math.Max(1, Math.Min(30, CleanupDays)), RegistryValueKind.DWord);
            }
        }
    }

    internal sealed class TemporaryExportStore
    {
        public string RootDirectory { get; private set; }

        public TemporaryExportStore()
        {
            RootDirectory = Path.Combine(
                SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.LocalApplicationData),
                "SolidWorksToBambu",
                "exports");
        }

        public string CreatePath(string documentName, string configurationName)
        {
            Directory.CreateDirectory(RootDirectory);
            string cleanDocument = Sanitize(Path.GetFileNameWithoutExtension(documentName));
            string cleanConfiguration = Sanitize(configurationName);
            string baseName = string.IsNullOrWhiteSpace(cleanConfiguration)
                ? cleanDocument
                : cleanDocument + "_" + cleanConfiguration;
            if (baseName.Length > 80)
            {
                baseName = baseName.Substring(0, 80);
            }

            string suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return Path.Combine(RootDirectory, baseName + "_" + suffix + ".stl");
        }

        public void CleanupOldFiles(int days)
        {
            if (!Directory.Exists(RootDirectory))
            {
                return;
            }

            DateTime cutoff = DateTime.Now.AddDays(-Math.Max(1, days));
            foreach (string file in Directory.EnumerateFiles(RootDirectory, "*.stl", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    FileInfo info = new FileInfo(file);
                    if (info.LastWriteTime < cutoff)
                    {
                        info.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("清理旧缓存失败: " + file, ex);
                }
            }
        }

        private static string Sanitize(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "SolidWorksPart" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] result = source.Select(c => invalid.Contains(c) || c == '*' ? '_' : c).ToArray();
            string sanitized = new string(result).Trim('.', ' ');
            return string.IsNullOrWhiteSpace(sanitized) ? "SolidWorksPart" : sanitized;
        }
    }

    internal static class BambuStudioLocator
    {
        private static readonly string[] ExecutableNames = { "bambu-studio.exe", "BambuStudio.exe" };

        public static string Find(string configuredPath)
        {
            foreach (string candidate in EnumerateCandidates(configuredPath))
            {
                string normalized = NormalizeExecutablePath(candidate);
                if (!string.IsNullOrWhiteSpace(normalized) && File.Exists(normalized))
                {
                    return Path.GetFullPath(normalized);
                }
            }

            return null;
        }

        public static void Launch(string executablePath, string modelPath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("找不到 Bambu Studio 可执行文件。", executablePath);
            }

            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                throw new FileNotFoundException("找不到要导入的临时 STL 文件。", modelPath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = Quote(modelPath),
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private static IEnumerable<string> EnumerateCandidates(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                yield return configuredPath;
            }

            string environmentPath = SystemEnvironment.GetEnvironmentVariable("BAMBU_STUDIO_PATH");
            if (!string.IsNullOrWhiteSpace(environmentPath))
            {
                yield return environmentPath;
            }

            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    foreach (string executableName in ExecutableNames)
                    {
                        string appPath = ReadDefaultRegistryValue(
                            hive,
                            view,
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + executableName);
                        if (!string.IsNullOrWhiteSpace(appPath))
                        {
                            yield return appPath;
                        }
                    }

                    foreach (string uninstallPath in ReadUninstallCandidates(hive, view))
                    {
                        yield return uninstallPath;
                    }
                }
            }

            string programFiles = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFiles);
            string programFilesX86 = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.ProgramFilesX86);
            string localAppData = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.LocalApplicationData);
            foreach (string root in new[]
            {
                Path.Combine(programFiles, "Bambu Studio"),
                Path.Combine(programFilesX86, "Bambu Studio"),
                Path.Combine(localAppData, "Programs", "Bambu Studio"),
                Path.Combine(localAppData, "Bambu Studio")
            })
            {
                foreach (string executableName in ExecutableNames)
                {
                    yield return Path.Combine(root, executableName);
                }
            }
        }

        private static IEnumerable<string> ReadUninstallCandidates(RegistryHive hive, RegistryView view)
        {
            const string uninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey root = baseKey.OpenSubKey(uninstallRoot, false))
            {
                if (root == null)
                {
                    yield break;
                }

                foreach (string subKeyName in root.GetSubKeyNames())
                {
                    using (RegistryKey key = root.OpenSubKey(subKeyName, false))
                    {
                        if (key == null)
                        {
                            continue;
                        }

                        string displayName = Convert.ToString(key.GetValue("DisplayName", string.Empty));
                        if (displayName.IndexOf("Bambu Studio", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        string displayIcon = Convert.ToString(key.GetValue("DisplayIcon", string.Empty));
                        if (!string.IsNullOrWhiteSpace(displayIcon))
                        {
                            yield return displayIcon;
                        }

                        string installLocation = Convert.ToString(key.GetValue("InstallLocation", string.Empty));
                        if (!string.IsNullOrWhiteSpace(installLocation))
                        {
                            foreach (string executableName in ExecutableNames)
                            {
                                yield return Path.Combine(installLocation, executableName);
                            }
                        }
                    }
                }
            }
        }

        private static string ReadDefaultRegistryValue(RegistryHive hive, RegistryView view, string subKey)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                {
                    return key == null ? null : Convert.ToString(key.GetValue(null, string.Empty));
                }
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeExecutablePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string result = SystemEnvironment.ExpandEnvironmentVariables(value.Trim());
            if (result.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = result.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    result = result.Substring(1, closingQuote - 1);
                }
            }
            else
            {
                int iconIndex;
                int comma = result.LastIndexOf(',');
                if (comma > 0 && int.TryParse(result.Substring(comma + 1).Trim(), out iconIndex))
                {
                    result = result.Substring(0, comma);
                }
            }

            return result.Trim().Trim('"');
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class StlExportService
    {
        private readonly ISldWorks _application;
        private readonly TemporaryExportStore _store;

        public StlExportService(ISldWorks application, TemporaryExportStore store)
        {
            if (application == null)
            {
                throw new ArgumentNullException("application");
            }

            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            _application = application;
            _store = store;
        }

        public string ExportActivePart()
        {
            IModelDoc2 model = _application.ActiveDoc as IModelDoc2;
            if (model == null)
            {
                throw new InvalidOperationException("请先打开一个 SOLIDWORKS 零件。");
            }

            if (model.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                throw new InvalidOperationException("当前版本只支持零件（.SLDPRT）。请切换到零件文档后重试。");
            }

            string title = model.GetTitle();
            string configuration = string.Empty;
            try
            {
                IConfiguration activeConfiguration = model.ConfigurationManager.ActiveConfiguration;
                configuration = activeConfiguration == null ? string.Empty : activeConfiguration.Name;
            }
            catch
            {
                // A configuration name is helpful but not required for export.
            }

            string outputPath = _store.CreatePath(title, configuration);
            model.ForceRebuild3(false);
            model.ClearSelection2(true);

            int errors = 0;
            int warnings = 0;
            bool success;
            using (new StlPreferenceScope(_application))
            {
                success = model.Extension.SaveAs3(
                    outputPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    null,
                    ref errors,
                    ref warnings);
            }

            if (!success || errors != 0 || !File.Exists(outputPath))
            {
                TryDelete(outputPath);
                throw new InvalidOperationException(
                    "SOLIDWORKS 导出 STL 失败。错误代码: " + errors + "，警告代码: " + warnings + "。");
            }

            Logger.Info("已导出临时 STL: " + outputPath + " (warnings=" + warnings + ")");
            return outputPath;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort after a failed export.
            }
        }

        private sealed class StlPreferenceScope : IDisposable
        {
            private readonly ISldWorks _application;
            private readonly bool _binary;
            private readonly bool _showInfo;
            private readonly bool _preview;
            private readonly int _units;
            private bool _disposed;

            public StlPreferenceScope(ISldWorks application)
            {
                _application = application;
                _binary = application.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
                _showInfo = application.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave);
                _preview = application.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
                _units = application.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);

                application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
                application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);
                application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);
                application.SetUserPreferenceIntegerValue(
                    (int)swUserPreferenceIntegerValue_e.swExportStlUnits,
                    (int)swLengthUnit_e.swMM);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                try
                {
                    _application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, _binary);
                    _application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, _showInfo);
                    _application.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, _preview);
                    _application.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, _units);
                }
                catch (Exception ex)
                {
                    Logger.Error("恢复 SOLIDWORKS STL 设置失败", ex);
                }
            }
        }
    }
}
