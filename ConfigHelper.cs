using System.Configuration;

namespace IndustrialInspectionWPF
{
    public static class ConfigHelper
    {
        public static string Get(string key, string def) => ConfigurationManager.AppSettings[key] ?? def;
        public static void Set(string key, string val)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, val);
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static string PlcIP => Get("PlcIP", "127.0.0.1");
        public static short PlcRack => short.Parse(Get("PlcRack", "0"));
        public static short PlcSlot => short.Parse(Get("PlcSlot", "1"));
        public static int DbNum => int.Parse(Get("DbNum", "3010"));

        // === 关键修改点：直接返回 false，彻底屏蔽仿真模式 ===
        public static bool IsSimulation => false;

        // 业务指令
        public static int Cmd_Reset_Req => int.Parse(Get("Cmd_Reset_Req", "503"));
        public static int Cmd_Reset_Ack => int.Parse(Get("Cmd_Reset_Ack", "504"));
        public static int Cmd_Auto_Req => int.Parse(Get("Cmd_Auto_Req", "505"));
        public static int Cmd_Auto_Ack => int.Parse(Get("Cmd_Auto_Ack", "506"));
        public static int Cmd_Data_Ack => int.Parse(Get("Cmd_Data_Ack", "500"));
        public static int Cmd_Recheck_Req => int.Parse(Get("Cmd_Recheck_Req", "507"));
        public static int Cmd_Recheck_Ack => int.Parse(Get("Cmd_Recheck_Ack", "508"));
        public static int Cmd_Eject_Req => int.Parse(Get("Cmd_Eject_Req", "509"));
        public static int Cmd_Eject_Ack => int.Parse(Get("Cmd_Eject_Ack", "510"));
        public static int Cmd_Ready => int.Parse(Get("Cmd_Ready", "501"));
    }
}