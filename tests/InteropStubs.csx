// Minimal compile-only substitutes for the SOLIDWORKS interop assemblies.
// The real build uses the official DLLs from the SOLIDWORKS 2025 installation.
namespace SolidWorks.Interop.swpublished
{
    public interface ISwAddin
    {
        bool ConnectToSW(object application, int cookie);
        bool DisconnectFromSW();
    }

    public interface SwAddin : ISwAddin
    {
    }
}

namespace SolidWorksTools
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class SwAddinAttribute : System.Attribute
    {
        public string Description;
        public string Title;
        public bool LoadAtStartup;
    }
}

namespace SolidWorks.Interop.sldworks
{
    public interface ISldWorks
    {
        object ActiveDoc { get; }
        bool SetAddinCallbackInfo2(int reserved, object callback, int cookie);
        ICommandManager GetCommandManager(int cookie);
        int GetImageSize(out int small, out int medium, out int large);
        bool GetUserPreferenceToggle(int preference);
        int GetUserPreferenceIntegerValue(int preference);
        bool SetUserPreferenceToggle(int preference, bool enabled);
        bool SetUserPreferenceIntegerValue(int preference, int value);
    }

    public interface IModelDoc2
    {
        int GetType();
        string GetTitle();
        IConfigurationManager ConfigurationManager { get; }
        bool ForceRebuild3(bool topOnly);
        void ClearSelection2(bool all);
        IModelDocExtension Extension { get; }
    }

    public interface IConfigurationManager
    {
        IConfiguration ActiveConfiguration { get; }
    }

    public interface IConfiguration
    {
        string Name { get; }
    }

    public interface IModelDocExtension
    {
        bool SaveAs3(string name, int version, int options, object exportData, object advancedOptions, ref int errors, ref int warnings);
    }

    public interface ICommandManager
    {
        ICommandGroup CreateCommandGroup2(int userId, string title, string toolTip, string hint, int position, bool ignorePrevious, ref int errors);
        bool RemoveCommandGroup2(int userId, bool runtimeOnly);
    }

    public interface ICommandGroup
    {
        object IconList { get; set; }
        object MainIconList { get; set; }
        bool HasToolbar { get; set; }
        bool HasMenu { get; set; }
        int AddCommandItem2(string name, int position, string hint, string toolTip, int imageIndex, string callback, string enableMethod, int userId, int itemType);
        void Activate();
    }
}

namespace SolidWorks.Interop.swconst
{
    public enum swDocumentTypes_e { swDocPART = 1 }
    public enum swSaveAsVersion_e { swSaveAsCurrentVersion = 0 }
    public enum swSaveAsOptions_e { swSaveAsOptions_Silent = 1 }
    public enum swUserPreferenceToggle_e
    {
        swSTLBinaryFormat,
        swSTLShowInfoOnSave,
        swSTLPreview
    }
    public enum swUserPreferenceIntegerValue_e { swExportStlUnits }
    public enum swLengthUnit_e { swMM }
    public enum swCommandItemType_e
    {
        swMenuItem = 1,
        swToolbarItem = 2
    }
}
