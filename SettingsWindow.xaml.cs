using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace IndustrialInspectionWPF
{
    public partial class SettingsWindow : Window
    {
        private PlcClient _plc;

        public SettingsWindow(PlcClient plc)
        {
            InitializeComponent();
            _plc = plc;
            LoadValues();
            LoadAddressTable();
        }

        private void LoadValues()
        {
            TxtIP.Text = ConfigHelper.PlcIP;
            TxtRack.Text = ConfigHelper.PlcRack.ToString();
            TxtSlot.Text = ConfigHelper.PlcSlot.ToString();
            TxtDbNum.Text = ConfigHelper.DbNum.ToString();

            TxtResetReq.Text = ConfigHelper.Cmd_Reset_Req.ToString();
            TxtResetAck.Text = ConfigHelper.Cmd_Reset_Ack.ToString();
            TxtAutoReq.Text = ConfigHelper.Cmd_Auto_Req.ToString();
            TxtAutoAck.Text = ConfigHelper.Cmd_Auto_Ack.ToString();
            TxtRecheckReq.Text = ConfigHelper.Cmd_Recheck_Req.ToString();
            TxtRecheckAck.Text = ConfigHelper.Cmd_Recheck_Ack.ToString();
            TxtEjectReq.Text = ConfigHelper.Cmd_Eject_Req.ToString();
            TxtEjectAck.Text = ConfigHelper.Cmd_Eject_Ack.ToString();
            TxtReady.Text = ConfigHelper.Cmd_Ready.ToString();
            TxtDataAck.Text = ConfigHelper.Cmd_Data_Ack.ToString();
        }

        private void LoadAddressTable()
        {
            string db = "DB" + ConfigHelper.DbNum;
            var list = new List<AddrItem>
            {
                new AddrItem("Reset_Req",     $"{db}.DBW0",   "Int",  "写", "复位请求", 0, -1),
                new AddrItem("Mode_Req",      $"{db}.DBW2",   "Int",  "写", "其他模式请求", 2, -1),
                new AddrItem("Op_ID",         $"{db}.DBW4",   "Int",  "写", "操作ID", 4, -1),
                new AddrItem("Handshake_Ack", $"{db}.DBX6.0", "Bool", "写", "握手确认", 6, 0),
                new AddrItem("Manual_Trig",   $"{db}.DBX6.1", "Bool", "写", "动作触发", 6, 1),

                new AddrItem("Reset_Status",  $"{db}.DBW10",  "Int",  "读", "复位反馈", 10, -1),
                new AddrItem("Mode_Status",   $"{db}.DBW12",  "Int",  "读", "模式反馈", 12, -1),
                new AddrItem("Auto_ID",       $"{db}.DBW14",  "Int",  "读", "检测ID", 14, -1),
                new AddrItem("Auto_Result",   $"{db}.DBW16",  "Int",  "读", "检测结果", 16, -1),
                new AddrItem("Data_Ready",    $"{db}.DBX18.0","Bool", "读", "数据就绪", 18, 0),
                new AddrItem("Pos_Ready",     $"{db}.DBX18.1","Bool", "读", "机构到位", 18, 1)
            };
            GridAddr.ItemsSource = list;
        }

        // === 修复后的读取逻辑 ===
        private void BtnRead_Click(object sender, RoutedEventArgs e)
        {
            if (_plc == null || !_plc.IsConnected)
            {
                MessageBox.Show("读取失败：PLC 尚未连接！");
                return;
            }

            if (_plc.LatestBuffer == null || _plc.LatestBuffer.Length < 24)
            {
                MessageBox.Show("读取失败：无有效数据，请检查网络或 DB 块！");
                return;
            }

            var btn = sender as Button;
            var item = btn.DataContext as AddrItem;
            if (item == null) return;

            try
            {
                byte[] buffer = _plc.LatestBuffer;

                if (item.Type == "Int")
                {
                    int val = (buffer[item.Offset] << 8) + buffer[item.Offset + 1];
                    item.CurrentValue = val.ToString();
                }
                else if (item.Type == "Bool")
                {
                    bool val = (buffer[item.Offset] & (1 << item.BitIndex)) > 0;
                    item.CurrentValue = val.ToString();
                }
            }
            catch (Exception ex)
            {
                item.CurrentValue = "Err";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ConfigHelper.Set("PlcIP", TxtIP.Text);
            ConfigHelper.Set("PlcRack", TxtRack.Text);
            ConfigHelper.Set("PlcSlot", TxtSlot.Text);
            ConfigHelper.Set("DbNum", TxtDbNum.Text);

            ConfigHelper.Set("Cmd_Reset_Req", TxtResetReq.Text);
            ConfigHelper.Set("Cmd_Reset_Ack", TxtResetAck.Text);
            ConfigHelper.Set("Cmd_Auto_Req", TxtAutoReq.Text);
            ConfigHelper.Set("Cmd_Auto_Ack", TxtAutoAck.Text);
            ConfigHelper.Set("Cmd_Recheck_Req", TxtRecheckReq.Text);
            ConfigHelper.Set("Cmd_Recheck_Ack", TxtRecheckAck.Text);
            ConfigHelper.Set("Cmd_Eject_Req", TxtEjectReq.Text);
            ConfigHelper.Set("Cmd_Eject_Ack", TxtEjectAck.Text);
            ConfigHelper.Set("Cmd_Ready", TxtReady.Text);
            ConfigHelper.Set("Cmd_Data_Ack", TxtDataAck.Text);

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        public class AddrItem : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public string Address { get; set; }
            public string Type { get; set; }
            public string Direction { get; set; }
            public string Desc { get; set; }
            public int Offset { get; set; }
            public int BitIndex { get; set; }

            private string _currentValue = "-";
            public string CurrentValue
            {
                get => _currentValue;
                set { _currentValue = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentValue")); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public AddrItem(string n, string a, string t, string d, string desc, int off, int bit)
            {
                Name = n; Address = a; Type = t; Direction = d; Desc = desc; Offset = off; BitIndex = bit;
            }
        }
    }
}