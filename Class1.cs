using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace IndustrialInspectionWPF
{
    // 定义数据结构：用于序列化保存
    public class RingStateData
    {
        public int ID { get; set; }
        public int Status { get; set; } // 0=白, 1=绿(OK), 2=红(NG)
    }

    public static class RingStateManager
    {
        private static string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RingState.xml");

        // 保存所有圆环状态
        public static void SaveState(Dictionary<int, RingControl> rings)
        {
            try
            {
                var list = new List<RingStateData>();
                foreach (var kvp in rings)
                {
                    // RingControl 需要加一个 Status 属性来获取当前颜色状态
                    // 这里我们假设 RingControl 已经有了 int Status 属性
                    list.Add(new RingStateData { ID = kvp.Key, Status = kvp.Value.CurrentStatus });
                }

                XmlSerializer serializer = new XmlSerializer(typeof(List<RingStateData>));
                using (TextWriter writer = new StreamWriter(FilePath))
                {
                    serializer.Serialize(writer, list);
                }
            }
            catch (Exception) { /* 忽略保存错误，防止卡顿 */ }
        }

        // 读取状态
        public static List<RingStateData> LoadState()
        {
            if (!File.Exists(FilePath)) return new List<RingStateData>();

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<RingStateData>));
                using (TextReader reader = new StreamReader(FilePath))
                {
                    return (List<RingStateData>)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return new List<RingStateData>();
            }
        }
    }
}