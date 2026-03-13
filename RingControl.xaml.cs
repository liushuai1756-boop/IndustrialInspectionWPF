using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IndustrialInspectionWPF
{
    public partial class RingControl : UserControl
    {
        public int ID { get; private set; }

        // 当前状态码 (用于保存到XML)
        // 0=初始, 1=自动OK, 2=自动NG
        // 3=复检OK, 4=复检NG, 5=已剔除
        public int CurrentStatus { get; private set; } = 0;

        public event Action<RingControl> RingClicked;

        public RingControl()
        {
            InitializeComponent();
        }

        public void Init(int id)
        {
            ID = id;
            TxtID.Text = id.ToString();
        }

        // === 核心：设置颜色并自动推导状态码 ===
        public void SetColor(Brush inner, Brush outer)
        {
            Inner.Fill = inner;
            Outer.Fill = outer;

            // 根据颜色组合，反推状态码以便保存
            // 1. 自动模式 (外环白色)
            if (IsBrush(outer, Brushes.White))
            {
                if (IsBrush(inner, Brushes.LimeGreen)) CurrentStatus = 1;      // 自动OK
                else if (IsBrush(inner, Brushes.Red)) CurrentStatus = 2;       // 自动NG
                else CurrentStatus = 0;                                        // 初始
            }
            // 2. 复检/剔除模式 (外环蓝色)
            else if (IsBrush(outer, Brushes.DodgerBlue))
            {
                if (IsBrush(inner, Brushes.LimeGreen)) CurrentStatus = 3;      // 复检OK
                else if (IsBrush(inner, Brushes.Red)) CurrentStatus = 4;       // 复检NG
                else if (IsBrush(inner, Brushes.White)) CurrentStatus = 5;     // 已剔除
            }
            // 其他情况归为0
            else
            {
                CurrentStatus = 0;
            }
        }

        // === 新增：根据状态码恢复颜色 (用于软件重启后) ===
        public void RestoreStatus(int status)
        {
            CurrentStatus = status;
            switch (status)
            {
                case 1: SetColorInternal(Brushes.LimeGreen, Brushes.White); break;
                case 2: SetColorInternal(Brushes.Red, Brushes.White); break;

                case 3: SetColorInternal(Brushes.LimeGreen, Brushes.DodgerBlue); break; // 恢复复检OK
                case 4: SetColorInternal(Brushes.Red, Brushes.DodgerBlue); break;       // 恢复复检NG
                case 5: SetColorInternal(Brushes.White, Brushes.DodgerBlue); break;     // 恢复已剔除

                default: SetColorInternal(Brushes.White, Brushes.White); break;
            }
        }

        // 内部设置颜色 (不改变 CurrentStatus，防止递归逻辑错误)
        private void SetColorInternal(Brush inner, Brush outer)
        {
            Inner.Fill = inner;
            Outer.Fill = outer;
        }

        public void Reset()
        {
            SetColor(Brushes.White, Brushes.White);
        }

        private void OnMouseClick(object sender, MouseButtonEventArgs e)
        {
            RingClicked?.Invoke(this);
        }

        // 辅助：比较两个 Brush 是否相同 (通过 Hex 字符串比较更稳健)
        private bool IsBrush(Brush a, Brush b)
        {
            return a.ToString() == b.ToString();
        }
    }
}