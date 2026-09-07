using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SystemEnvironment = System.Environment;

namespace SolidWorksToBambu
{
    [ComVisible(true)]
    [Guid(AddinGuid)]
    [SolidWorksTools.SwAddin(
        Description = "将当前 SOLIDWORKS 零件一键发送到 Bambu Studio",
        Title = "SolidWorks → Bambu Studio",
        LoadAtStartup = true)]
    public class SwAddin : SolidWorks.Interop.swpublished.SwAddin
    {
        public const string AddinGuid = "B759D7C6-C514-4A51-9C81-56047875A846";
        private const string AddinTitle = "SolidWorks → Bambu Studio";
        private const string AddinDescription = "将当前 SOLIDWORKS 零件一键发送到 Bambu Studio";
        private const int CommandGroupId = 18437;
        private const int SendCommandId = 1;
        private const int SettingsCommandId = 2;

        private ISldWorks _application;
        private ICommandManager _commandManager;
        private int _cookie;

        [ComRegisterFunction]
        public static void Register(Type type)
        {
            string guid = "{" + type.GUID.ToString().ToUpperInvariant() + "}";
            using (RegistryKey addinKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + guid))
            {
                if (addinKey == null)
                {
                    throw new InvalidOperationException("无法创建 SOLIDWORKS 加载项注册表项。");
                }

                // The SOLIDWORKS .NET add-in template registers 0 here. Startup
                // preference is stored separately under the current user key.
                addinKey.SetValue(null, 0, RegistryValueKind.DWord);
                addinKey.SetValue("Title", AddinTitle, RegistryValueKind.String);
                addinKey.SetValue("Description", AddinDescription, RegistryValueKind.String);
            }

            using (RegistryKey startupKey = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\" + guid))
            {
                if (startupKey != null)
                {
                    startupKey.SetValue(null, 1, RegistryValueKind.DWord);
                }
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type type)
        {
            string guid = "{" + type.GUID.ToString().ToUpperInvariant() + "}";
            DeleteSubKeyIfPresent(Registry.LocalMachine, @"SOFTWARE\SolidWorks\Addins\" + guid);
            DeleteSubKeyIfPresent(Registry.CurrentUser, @"Software\SolidWorks\AddInsStartup\" + guid);
        }

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                Logger.Info("ConnectToSW callback started, cookie=" + Cookie);
                _application = (ISldWorks)ThisSW;
                _cookie = Cookie;
                _application.SetAddinCallbackInfo2(0, this, _cookie);
                _commandManager = _application.GetCommandManager(_cookie);
                AddCommandManager();
                Logger.Info("加载项已连接到 SOLIDWORKS");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("加载项连接失败", ex);
                MessageBox.Show("SolidWorks → Bambu Studio 加载失败：" + ex.Message, AddinTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                if (_commandManager != null)
                {
                    _commandManager.RemoveCommandGroup2(CommandGroupId, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("移除命令组失败", ex);
            }
            finally
            {
                ReleaseComObject(_commandManager);
                _commandManager = null;
                ReleaseComObject(_application);
                _application = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return true;
        }

        public void SendToBambuStudio()
        {
            string exportedPath = null;
            string backupPath = null;
            try
            {
                PluginSettings settings = PluginSettings.Load();
                string bambuStudioPath = BambuStudioLocator.Find(settings.BambuStudioPath);
                if (string.IsNullOrWhiteSpace(bambuStudioPath))
                {
                    DialogResult choose = MessageBox.Show(
                        "未找到 Bambu Studio。是否现在指定 bambu-studio.exe 的位置？",
                        AddinTitle,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (choose != DialogResult.Yes)
                    {
                        return;
                    }

                    using (SettingsForm form = new SettingsForm(settings))
                    {
                        if (form.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                    }

                    settings = PluginSettings.Load();
                    bambuStudioPath = BambuStudioLocator.Find(settings.BambuStudioPath);
                    if (string.IsNullOrWhiteSpace(bambuStudioPath))
                    {
                        throw new FileNotFoundException("仍未找到 Bambu Studio 可执行文件。");
                    }
                }

                TemporaryExportStore store = new TemporaryExportStore();
                if (!settings.KeepExportFiles)
                {
                    store.CleanupOldFiles(settings.CleanupDays);
                }

                ThreeMfExportService exporter = new ThreeMfExportService(_application, store);
                string documentName;
                exportedPath = exporter.ExportActivePart(out documentName);
                MonthlyBackupStore backupStore = new MonthlyBackupStore();
                backupPath = backupStore.Backup(exportedPath, documentName);
                BambuStudioLocator.Launch(bambuStudioPath, exportedPath);
                Logger.Info("已启动 Bambu Studio: " + bambuStudioPath + " (backup=" + backupPath + ")");
            }
            catch (Exception ex)
            {
                Logger.Error("发送到 Bambu Studio 失败", ex);
                string cacheHint = string.IsNullOrWhiteSpace(exportedPath)
                    ? string.Empty
                    : SystemEnvironment.NewLine + "临时文件保留在：" + exportedPath;
                string backupHint = string.IsNullOrWhiteSpace(backupPath)
                    ? string.Empty
                    : SystemEnvironment.NewLine + "备份文件保留在：" + backupPath;
                MessageBox.Show(
                    "发送到 Bambu Studio 失败：" + ex.Message + cacheHint + backupHint,
                    AddinTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public int CanSendToBambuStudio()
        {
            try
            {
                IModelDoc2 model = _application == null ? null : _application.ActiveDoc as IModelDoc2;
                return model != null && model.GetType() == (int)swDocumentTypes_e.swDocPART ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        public void ShowSettings()
        {
            try
            {
                using (SettingsForm form = new SettingsForm(PluginSettings.Load()))
                {
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("打开设置失败", ex);
                MessageBox.Show("无法打开设置：" + ex.Message, AddinTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public int AlwaysEnabled()
        {
            return 1;
        }

        private void AddCommandManager()
        {
            int errors = 0;
            ICommandGroup group = _commandManager.CreateCommandGroup2(
                CommandGroupId,
                "Bambu Studio",
                "发送到 Bambu Studio",
                "将当前零件发送到 Bambu Studio",
                -1,
                false,
                ref errors);
            if (group == null)
            {
                throw new InvalidOperationException("SOLIDWORKS 无法创建 Bambu Studio 命令组。错误代码: " + errors);
            }

            int small = 20;
            int medium = 32;
            int large = 40;
            try
            {
                _application.GetImageSize(out small, out medium, out large);
            }
            catch (Exception ex)
            {
                Logger.Error("读取 SOLIDWORKS 图标尺寸失败，使用默认尺寸", ex);
            }

            string[] commandIcons;
            string[] mainIcons;
            IconFactory.CreateIconLists(small, medium, large, out commandIcons, out mainIcons);
            group.IconList = commandIcons;
            group.MainIconList = mainIcons;

            int menuAndToolbar = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            int menuOnly = (int)swCommandItemType_e.swMenuItem;
            group.AddCommandItem2(
                "发送到 Bambu Studio",
                -1,
                "导出当前零件并在 Bambu Studio 中打开",
                "发送到 Bambu Studio",
                0,
                "SendToBambuStudio",
                "CanSendToBambuStudio",
                SendCommandId,
                menuAndToolbar);
            group.AddCommandItem2(
                "设置…",
                -1,
                "设置 Bambu Studio 路径和临时文件清理策略",
                "SolidWorks → Bambu Studio 设置",
                1,
                "ShowSettings",
                "AlwaysEnabled",
                SettingsCommandId,
                menuOnly);

            group.HasToolbar = true;
            group.HasMenu = true;
            group.Activate();
            Logger.Info("命令组创建完成，errors=" + errors);
        }

        private static void DeleteSubKeyIfPresent(RegistryKey root, string path)
        {
            try
            {
                using (RegistryKey existing = root.OpenSubKey(path, false))
                {
                    if (existing == null)
                    {
                        return;
                    }
                }

                root.DeleteSubKeyTree(path, false);
            }
            catch
            {
                // RegAsm uninstall should be idempotent.
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }
}
