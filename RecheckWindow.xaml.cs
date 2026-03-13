using System.Windows;

namespace IndustrialInspectionWPF
{
    public partial class RecheckWindow : Window
    {
        // True = OK, False = NG, Null = Cancel
        public bool? Result { get; private set; } = null;

        public RecheckWindow(int id)
        {
            InitializeComponent();
            TxtMsg.Text = $"请确认位置 #{id} 的复检结果";
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            Result = true; // OK
            this.Close();
        }

        private void BtnNG_Click(object sender, RoutedEventArgs e)
        {
            Result = false; // NG
            this.Close();
        }
    }
}