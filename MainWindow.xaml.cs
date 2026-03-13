using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IndustrialInspectionWPF
{
    public enum SysMode { None, Auto, Recheck, Eject }

    public partial class MainWindow : Window
    {
        private PlcClient _plc;
        private Dictionary<int, RingControl> _rings = new Dictionary<int, RingControl>();
        private SysMode _currentMode = SysMode.None;
        private SysMode _targetMode = SysMode.None;
        private RingControl _pendingRing = null;
        private bool _isLogicBusy = false; // 防呆锁

        private ToastWindow _activeToast = null;
        private LogWindow _logWin = null;
        private StringBuilder _logCache = new StringBuilder();
        private int _countOK = 0, _countNG = 0;

        const double Spacing = 48; const double StartX = 50; const double BottomBase = 420;

        public MainWindow()
        {
            InitializeComponent();
            InitGridWithLabels();
            RestoreRingStates();
            this.Closed += MainWindow_Closed;

            _plc = new PlcClient();
            _plc.OnLog += msg => Dispatcher.Invoke(() => AppendLog(msg));
            _plc.OnModeChanged += code => Dispatcher.Invoke(() => HandleModeAck(code));
            _plc.OnAutoData += (id, res) => Dispatcher.Invoke(() => HandleAutoData(id, res));
            _plc.OnPosReady += () => Dispatcher.Invoke(() => HandlePosReady());
            _plc.OnConnected += () => Dispatcher.Invoke(() => UpdateCommStatus(true));
            _plc.OnDisconnected += () => Dispatcher.Invoke(() => UpdateCommStatus(false));

            _plc.Start();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_plc != null) _plc.Stop();
            if (_logWin != null) _logWin.Close();
            Application.Current.Shutdown();
        }

        // --- 按钮点击 (带状态清洗) ---
        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            _isLogicBusy = false; // 强制解锁，防止死锁
            _pendingRing = null;
            _plc.ResetOpTrigger();

            SysMode newTarget = SysMode.None;
            int reqCode = 0, waitCode = 0, currentVal = 0;

            if (sender == BtnReset)
            {
                newTarget = SysMode.None; reqCode = ConfigHelper.Cmd_Reset_Req; waitCode = ConfigHelper.Cmd_Reset_Ack;
                currentVal = _plc.CurrentResetStatus;
                _targetMode = newTarget;
                _plc.WriteResetReq(reqCode);
            }
            else
            {
                if (sender == BtnAuto) { newTarget = SysMode.Auto; reqCode = ConfigHelper.Cmd_Auto_Req; waitCode = ConfigHelper.Cmd_Auto_Ack; }
                else if (sender == BtnRecheck) { newTarget = SysMode.Recheck; reqCode = ConfigHelper.Cmd_Recheck_Req; waitCode = ConfigHelper.Cmd_Recheck_Ack; }
                else if (sender == BtnEject) { newTarget = SysMode.Eject; reqCode = ConfigHelper.Cmd_Eject_Req; waitCode = ConfigHelper.Cmd_Eject_Ack; }

                currentVal = _plc.CurrentModeStatus;
                _targetMode = newTarget;
                _plc.WriteOtherModeReq(reqCode);
            }

            string modeName = GetModeName(newTarget);
            AppendLog($"[操作] 请求切换: {modeName} (发 {reqCode})");
            AppendLog($"[状态] 等待反馈: {waitCode}, 当前PLC值: {currentVal}");
        }

        private void HandleModeAck(int code)
        {
            bool isMatch = false;
            if (_targetMode == SysMode.None && code == ConfigHelper.Cmd_Reset_Ack) isMatch = true;
            else if (_targetMode == SysMode.Auto && code == ConfigHelper.Cmd_Auto_Ack) isMatch = true;
            else if (_targetMode == SysMode.Recheck && code == ConfigHelper.Cmd_Recheck_Ack) isMatch = true;
            else if (_targetMode == SysMode.Eject && code == ConfigHelper.Cmd_Eject_Ack) isMatch = true;

            if (isMatch)
            {
                SetUIActiveMode(_targetMode);
                AppendLog($"[成功] 模式切换完成: {_targetMode}");

                // === [恢复点] 显式调用清零 (因为底层不再自动循环清零) ===
                if (_targetMode == SysMode.None) _plc.ClearResetArea();
                else _plc.ClearModeArea();
            }
        }

        // --- 圆环点击 (带防呆锁) ---
        private void OnRingClick(RingControl ring)
        {
            if (_currentMode != SysMode.Recheck && _currentMode != SysMode.Eject) return;

            // 防呆：防止连续点击导致逻辑覆盖
            if (_isLogicBusy) return;

            if (_plc.IsPosReady)
            {
                AppendLog($"[严重警告] 到位信号 (DBX18.1) 卡死为 True！", Brushes.Red);
                return;
            }

            // 上锁
            _isLogicBusy = true;
            _pendingRing = ring;

            _plc.WriteOpId(ring.ID); // 发送

            ShowToast($"指令已发送 (#{ring.ID})，等待 PLC 到位...");
            AppendLog($"[操作] 点击 #{ring.ID} -> 发送 -> 等待 501");
        }

        private void HandlePosReady()
        {
            if (_activeToast != null) { try { _activeToast.Close(); } catch { } _activeToast = null; }
            if (_pendingRing != null) ShowConfirmDialog(_pendingRing);
        }

        private void ShowConfirmDialog(RingControl ring)
        {
            var currentRing = ring; _pendingRing = null; bool changed = false;

            if (_currentMode == SysMode.Recheck)
            {
                var dlg = new RecheckWindow(currentRing.ID); dlg.Owner = this; dlg.ShowDialog();
                if (dlg.Result == true) { currentRing.SetColor(Brushes.LimeGreen, Brushes.DodgerBlue); changed = true; }
                else if (dlg.Result == false) { currentRing.SetColor(Brushes.Red, Brushes.DodgerBlue); changed = true; }
            }
            else if (_currentMode == SysMode.Eject)
            {
                var dlg = new EjectWindow(currentRing.ID); dlg.Owner = this; dlg.ShowDialog();
                if (dlg.IsConfirmed) { currentRing.SetColor(Brushes.White, Brushes.DodgerBlue); changed = true; }
            }

            // 交互结束：复位触发信号
            _plc.ResetOpTrigger();

            if (changed) SaveAllRings();

            // 解锁
            _isLogicBusy = false;
        }

        // --- 自动数据 ---
        private void HandleAutoData(int rid, int st)
        {
            if (_currentMode == SysMode.Auto && _rings.ContainsKey(rid))
            {
                var brush = st == 1 ? Brushes.LimeGreen : Brushes.Red;
                _rings[rid].SetColor(brush, Brushes.White);
                if (st == 1) _countOK++; else _countNG++;
                UpdateStatsUI();

                // 握手确认 (一次性写入)
                _plc.WriteDataAck(true);
                SaveAllRings();
            }
        }

        // ... 辅助代码 ...
        private void SetUIActiveMode(SysMode mode)
        {
            _currentMode = mode;
            BtnReset.Tag = (mode == SysMode.None) ? "LimeGreen" : "Gold";
            BtnAuto.Tag = (mode == SysMode.Auto) ? "LimeGreen" : "Gold";
            BtnRecheck.Tag = (mode == SysMode.Recheck) ? "LimeGreen" : "Gold";
            BtnEject.Tag = (mode == SysMode.Eject) ? "LimeGreen" : "Gold";
            if (mode == SysMode.None || mode == SysMode.Auto)
            {
                if (mode == SysMode.None) foreach (var r in _rings.Values) r.Reset();
                _countOK = 0; _countNG = 0; UpdateStatsUI(); SaveAllRings();
            }
        }
        private void ShowToast(string msg) { if (_activeToast != null) { try { _activeToast.Close(); } catch { } } _activeToast = new ToastWindow(msg); _activeToast.Owner = this; _activeToast.Show(); }
        private void InitGridWithLabels() { GridCanvas.Children.Clear(); _rings.Clear(); for (int c = 1; c <= 27; c++) { double x = StartX + (c - 1) * Spacing; var txt = CreateLabel(c.ToString(), Brushes.Black); Canvas.SetLeft(txt, x + 10); Canvas.SetTop(txt, BottomBase + 45); GridCanvas.Children.Add(txt); } for (int r = 1; r <= 8; r++) { var txt = CreateLabel(r.ToString(), Brushes.Gray); Canvas.SetLeft(txt, 10); Canvas.SetTop(txt, BottomBase - (r - 1) * Spacing + 8); GridCanvas.Children.Add(txt); } for (int i = 0; i < 7; i++) { var txt = CreateLabel((7 - i).ToString(), Brushes.Gray); Canvas.SetLeft(txt, StartX + 27 * Spacing + 10); Canvas.SetTop(txt, (BottomBase - 6.5 * Spacing) + i * Spacing + 8); GridCanvas.Children.Add(txt); } int id = 1; for (int col = 1; col <= 27; col++) { int rows = (col == 27) ? 5 : ((col % 2 != 0) ? 8 : 7); double x = StartX + (col - 1) * Spacing; for (int i = 0; i < rows; i++) { var ring = new RingControl { Width = 38, Height = 38 }; ring.Init(id); ring.RingClicked += OnRingClick; double y = (col % 2 != 0) ? (BottomBase - i * Spacing) : ((BottomBase - 6.5 * Spacing) + i * Spacing); Canvas.SetLeft(ring, x); Canvas.SetTop(ring, y); GridCanvas.Children.Add(ring); _rings[id] = ring; id++; } } }
        private TextBlock CreateLabel(string text, Brush color) => new TextBlock { Text = text, FontWeight = FontWeights.Bold, Foreground = color, FontSize = 14 };
        private void BtnSettings_Click(object sender, RoutedEventArgs e) { var w = new SettingsWindow(_plc); w.Owner = this; if (w.ShowDialog() == true) MessageBox.Show("保存成功"); }
        private void BtnShowLog_Click(object sender, RoutedEventArgs e) { if (_logWin == null || !wIsLoaded(_logWin)) { _logWin = new LogWindow(); _logWin.AddLog(_logCache.ToString()); _logWin.Show(); } else _logWin.Activate(); }
        private bool wIsLoaded(Window w) => w.IsLoaded && w.IsVisible;
        private void UpdateStatsUI() { TxtOK.Text = _countOK.ToString(); TxtNG.Text = _countNG.ToString(); }
        private void AppendLog(string msg, Brush color = null) { string l = $"[{DateTime.Now:HH:mm:ss}] {msg}\n"; _logCache.Append(l); if (_logWin != null && wIsLoaded(_logWin)) _logWin.AddLog(l); }
        private void UpdateCommStatus(bool isConnected) { if (isConnected) { LedComm.Background = Brushes.LimeGreen; LedShadow.Color = Colors.LimeGreen; TxtComm.Text = "PLC 已连接"; TxtComm.Foreground = Brushes.Green; } else { LedComm.Background = Brushes.Gray; LedShadow.Color = Colors.Gray; TxtComm.Text = "PLC 未连接"; TxtComm.Foreground = Brushes.Gray; } }
        private void SaveAllRings() { RingStateManager.SaveState(_rings); }
        private void RestoreRingStates() { var savedData = RingStateManager.LoadState(); int ok = 0, ng = 0; foreach (var item in savedData) { if (_rings.ContainsKey(item.ID)) { _rings[item.ID].RestoreStatus(item.Status); if (item.Status == 1 || item.Status == 3) ok++; else if (item.Status == 2 || item.Status == 4) ng++; } } _countOK = ok; _countNG = ng; UpdateStatsUI(); AppendLog($"[系统] 已恢复上次状态 (OK:{_countOK}, NG:{_countNG})"); }
        private string GetModeName(SysMode m) { switch (m) { case SysMode.None: return "复位模式"; case SysMode.Auto: return "自动模式"; case SysMode.Recheck: return "复检模式"; case SysMode.Eject: return "剔除模式"; default: return "未知"; } }
    }
}