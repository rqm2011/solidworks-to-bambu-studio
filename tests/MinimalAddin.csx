using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("SolidWorks to Bambu Studio Minimal Load Check")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: ComVisible(false)]
[assembly: Guid("cb855668-83e2-4562-991b-60297553ef8c")]
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace SolidWorksToBambu
{
    [Guid("B759D7C6-C514-4A51-9C81-56047875A846")]
    [ComVisible(true)]
    [SolidWorksTools.SwAddin(
        Description = "Minimal load check",
        Title = "SolidWorks to Bambu Studio",
        LoadAtStartup = true)]
    public class SwAddin : SolidWorks.Interop.swpublished.SwAddin
    {
        public bool ConnectToSW(object application, int cookie)
        {
            return true;
        }

        public bool DisconnectFromSW()
        {
            return true;
        }
    }
}
