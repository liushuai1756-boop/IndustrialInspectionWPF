using System;
using System.Windows;

namespace IndustrialInspectionWPF
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        public void AddLog(string msg)
        {
            // 简单的追加逻辑
            TxtLogContent.AppendText(msg);
            TxtLogContent.ScrollToEnd();
        }
    }
}