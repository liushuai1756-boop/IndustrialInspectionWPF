using S7.Net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IndustrialInspectionWPF
{
    public class PlcClient
    {
        private Plc _s7Plc;
        private volatile bool _isConnected = false;
        private volatile bool _isStop = false;

        // 状态缓存
        private bool _wasConnected = false;
        private int _lastResetStatus = 0;
        private int _lastModeStatus = 0;
        private bool _lastDataReady = false;
        private bool _lastPosReady = false;

        // 公开属性 (供设置界面监控)
        public byte[] LatestBuffer { get; private set; } = new byte[24];
        public int CurrentResetStatus => _lastResetStatus;
        public int CurrentModeStatus => _lastModeStatus;
        public bool IsDataReady => _lastDataReady;
        public bool IsPosReady => _lastPosReady;

        // 事件
        public event Action<int> OnModeChanged;
        public event Action<int, int> OnAutoData;
        public event Action OnPosReady;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        public bool IsConnected => _isConnected;

        public PlcClient() { }

        public void Start()
        {
            _isStop = false;
            Task.Factory.StartNew(CommunicationLoop, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            _isStop = true;
            CloseConnection();
        }

        // ==========================================
        // 1. 写操作 API (恢复为直接写入)
        // ==========================================

        // 复位请求 (DBW0)
        public void WriteResetReq(int code)
        {
            ResetStateCache();
            SafeWriteInt(0, (short)code, "复位请求");
        }

        // [保留优点] 冗余写入：确保清零成功 (写两次)
        public void ClearResetArea()
        {
            OnLog?.Invoke($"[闭环] 清除复位请求 (DBW0=0)");
            SafeWriteInt(0, 0, "清除Req(1)");
            SafeWriteInt(0, 0, "清除Req(2)"); // 补刀
        }

        // 其他模式请求 (DBW2)
        public void WriteOtherModeReq(int code)
        {
            ResetStateCache();
            SafeWriteInt(2, (short)code, $"模式请求({code})");
        }

        // [保留优点] 冗余写入
        public void ClearModeArea()
        {
            OnLog?.Invoke($"[闭环] 清除模式请求 (DBW2=0)");
            SafeWriteInt(2, 0, "清除Req(1)");
            SafeWriteInt(2, 0, "清除Req(2)"); // 补刀
        }

        // 写操作ID (DBW4) + Trigger (DBX6.1)
        public void WriteOpId(int id)
        {
            OnLog?.Invoke($"[发送] ID: {id}, 触发动作");
            SafeWriteInt(4, (short)id, "操作ID");
            SafeWriteBool(6, 1, true, "Trigger置1");
        }

        // 握手确认 (DBX6.0)
        public void WriteDataAck(bool value)
        {
            SafeWriteBool(6, 0, value, "握手ACK");
        }

        // 复位触发信号 (DBX6.1)
        public void ResetOpTrigger()
        {
            SafeWriteBool(6, 1, false, "Trigger复位");
        }

        public void ResetStateCache()
        {
            _lastResetStatus = 0;
            _lastModeStatus = 0;
        }

        // ==========================================
        // 2. 核心通信循环 (只读，不负责写)
        // ==========================================
        private async Task CommunicationLoop()
        {
            while (!_isStop)
            {
                // A. 连接逻辑
                if (!_isConnected)
                {
                    await ConnectPlcAsync();
                }

                // B. 读取逻辑
                if (_isConnected && _s7Plc != null)
                {
                    try
                    {
                        // 读取 DB3010 前24个字节
                        byte[] buffer = await _s7Plc.ReadBytesAsync(DataType.DataBlock, ConfigHelper.DbNum, 0, 24);
                        ProcessPlcData(buffer);
                    }
                    catch (Exception ex)
                    {
                        if (_wasConnected) OnLog?.Invoke($"[通信异常] 读取失败: {ex.Message}");
                        ForceDisconnect();
                        await Task.Delay(2000);
                    }
                }

                CheckConnectionStatusChange();
                await Task.Delay(50); // 50ms 轮询
            }
        }

        private async Task ConnectPlcAsync()
        {
            try
            {
                CloseConnection();
                _s7Plc = new Plc(CpuType.S71200, ConfigHelper.PlcIP, ConfigHelper.PlcRack, ConfigHelper.PlcSlot);
                await _s7Plc.OpenAsync();

                if (_s7Plc.IsConnected)
                {
                    _isConnected = true;
                    await Task.Delay(200); // 握手缓冲
                }
            }
            catch
            {
                _isConnected = false;
                await Task.Delay(2000);
            }
        }

        private void ForceDisconnect()
        {
            _isConnected = false;
            CloseConnection();
        }

        private void CloseConnection()
        {
            try
            {
                if (_s7Plc != null) { _s7Plc.Close(); _s7Plc = null; }
            }
            catch { }
            _isConnected = false;
        }

        private void CheckConnectionStatusChange()
        {
            if (_isConnected != _wasConnected)
            {
                if (_isConnected)
                {
                    OnLog?.Invoke($"[系统] S7 PLC 连接成功 ({ConfigHelper.PlcIP})");
                    OnConnected?.Invoke();
                }
                else
                {
                    if (!_wasConnected) OnLog?.Invoke("[系统] S7 PLC 连接已断开");
                    OnDisconnected?.Invoke();
                }
                _wasConnected = _isConnected;
            }
        }

        // ==========================================
        // 3. 数据处理
        // ==========================================
        private void ProcessPlcData(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 24) return;
            LatestBuffer = buffer;

            // A. 复位反馈 (DBW10)
            int resetStatus = (buffer[10] << 8) + buffer[11];
            if (resetStatus != _lastResetStatus && resetStatus != 0)
            {
                _lastResetStatus = resetStatus;
                OnLog?.Invoke($"[PLC状态] 复位反馈: {resetStatus}");
                OnModeChanged?.Invoke(resetStatus);
            }

            // B. 模式反馈 (DBW12)
            int modeStatus = (buffer[12] << 8) + buffer[13];
            if (modeStatus != _lastModeStatus && modeStatus != 0)
            {
                _lastModeStatus = modeStatus;
                string desc = GetModeName(modeStatus);
                OnLog?.Invoke($"[PLC状态] 模式反馈: {desc} ({modeStatus})");
                OnModeChanged?.Invoke(modeStatus);
            }

            // C. 自动数据 (DBX18.0)
            bool dataReady = (buffer[18] & 0x01) > 0;
            if (dataReady && !_lastDataReady)
            {
                int autoId = (buffer[14] << 8) + buffer[15];
                int autoRes = (buffer[16] << 8) + buffer[17];
                string resStr = (autoRes == 1) ? "OK" : "NG";
                OnLog?.Invoke($"[自动数据] ID: {autoId}, 结果: {resStr}");
                OnAutoData?.Invoke(autoId, autoRes);
            }
            // 自动握手下降沿处理
            else if (!dataReady && _lastDataReady)
            {
                WriteDataAck(false); // 复位 ACK
            }
            _lastDataReady = dataReady;

            // D. 到位信号 (DBX18.1)
            bool posReady = (buffer[18] & 0x02) > 0;
            if (posReady && !_lastPosReady)
            {
                OnPosReady?.Invoke();
            }
            _lastPosReady = posReady;
        }

        private string GetModeName(int code)
        {
            if (code == ConfigHelper.Cmd_Reset_Req || code == ConfigHelper.Cmd_Reset_Ack) return "复位";
            if (code == ConfigHelper.Cmd_Auto_Req || code == ConfigHelper.Cmd_Auto_Ack) return "自动";
            if (code == ConfigHelper.Cmd_Recheck_Req || code == ConfigHelper.Cmd_Recheck_Ack) return "复检";
            if (code == ConfigHelper.Cmd_Eject_Req || code == ConfigHelper.Cmd_Eject_Ack) return "剔除";
            return "未知";
        }

        // ==========================================
        // 4. 安全写入
        // ==========================================
        private void SafeWriteInt(int offset, short value, string desc)
        {
            if (!_isConnected || _s7Plc == null) return;
            try
            {
                byte[] bytes = new byte[2];
                bytes[0] = (byte)(value >> 8); bytes[1] = (byte)(value & 0xFF);
                _s7Plc.WriteBytes(DataType.DataBlock, ConfigHelper.DbNum, offset, bytes);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[写入失败] {desc}: {ex.Message}");
                ForceDisconnect();
            }
        }

        private void SafeWriteBool(int offsetByte, int bitIndex, bool value, string desc)
        {
            if (!_isConnected || _s7Plc == null) return;
            try
            {
                _s7Plc.WriteBit(DataType.DataBlock, ConfigHelper.DbNum, offsetByte, bitIndex, value);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[写入失败] {desc}: {ex.Message}");
                ForceDisconnect();
            }
        }
    }
}