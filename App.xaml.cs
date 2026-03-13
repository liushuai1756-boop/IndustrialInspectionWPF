using System;
using System.Threading;
using System.Windows;

namespace IndustrialInspectionWPF
{
    public partial class App : Application
    {
        private static Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "IndustrialInspectionWPF_Unique_Mutex";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("程序已经在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
        }
    }
}