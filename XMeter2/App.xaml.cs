using System.Diagnostics;
using System.Windows;

namespace XMeter2
{
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

            base.OnStartup(e);
        }
    }
}
