using System;
using System.Windows;
using System.Windows.Threading;

namespace IndustrialInspectionWPF
{
    public partial class ToastWindow : Window
    {
        private DispatcherTimer _timer;

        public ToastWindow(string message)
        {
            InitializeComponent();
            TxtMsg.Text = message;

            // 设定 1秒 后自动关闭
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1.0);
            _timer.Tick += (s, e) => {
                _timer.Stop();
                this.Close();
            };
            _timer.Start();
        }
    }
}