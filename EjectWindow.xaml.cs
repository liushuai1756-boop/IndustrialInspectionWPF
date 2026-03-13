using System.Windows;

namespace IndustrialInspectionWPF
{
    public partial class EjectWindow : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public EjectWindow(int id)
        {
            InitializeComponent();
            TxtMsg.Text = $"确认剔除位置 #{id} ?";
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.Close();
        }
    }
}