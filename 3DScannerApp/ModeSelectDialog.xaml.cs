using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _3DScannerApp
{
    /// <summary>
    /// ModeSelectDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ModeSelectDialog : UserControl
    {
        private int Mode;
        public ModeSelectDialog(int mode)
        {
            Mode = mode;
            InitializeComponent();

            
            Start_Position.Text = Properties.Settings.Default.intervalStart;
            End_Position.Text = Properties.Settings.Default.intervalEnd;
            Scan_Increment.Text = Properties.Settings.Default.intervalIncrement;
            Delay_Time.Text = Properties.Settings.Default.intervalDelay;
            Stay_Time.Text = Properties.Settings.Default.intervalStay;
            Trigger_Checkbox.IsChecked = Properties.Settings.Default.intervalTrigger == "1" ? true : false;
        }

        // 获取和校验的结果
        private byte[] GetByteArray(int length, byte[] byArr)
        {
            byte[] byteArray = new byte[length];
            for (int i = 0; i < byArr.Length; i++)
            {
                byteArray[i] = byArr[i];
            }
            byteArray[byteArray.Length - 1] = ComputeChecksum(byArr);
            return byteArray;
        }

        static byte ComputeChecksum(byte[] data)
        {
            int sum = 0;

            // 累加所有字节
            foreach (byte b in data)
            {
                sum += b;
            }

            // 取结果的低字节
            byte checksum = (byte)(sum & 0xFF);

            byte lowByte = (byte)(checksum & 0xFF);

            return lowByte;
        }

        // 开始扫描
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Start_Position.Text))
            {
                MessageBox.Show("扫描起点不能为空！");
                return;
            }
            if (string.IsNullOrEmpty(End_Position.Text))
            {
                MessageBox.Show("扫描终点不能为空！");
                return;
            }
            
            if (float.Parse(Start_Position.Text) == float.Parse(End_Position.Text))
            {
                MessageBox.Show("扫描起点不能和扫描终点一样！");
                return;
            }
            
            if (string.IsNullOrEmpty(Stay_Time.Text))
            {
                MessageBox.Show("保持时间不能为空！");
                return;
            }
            if (float.Parse(Stay_Time.Text) < 0)
            {
                MessageBox.Show("保持时间不能为负数！");
                return;
            }
            
            // 当选择的是间隔触发模式
            if (string.IsNullOrEmpty(Scan_Increment.Text))
            {
                MessageBox.Show("扫描增量不能为空！");
                return;
            }
            if (float.Parse(Scan_Increment.Text) <= 0)
            {
                MessageBox.Show("扫描增量不能为负数！");
                return;
            }
            if (string.IsNullOrEmpty(Delay_Time.Text))
            {
                MessageBox.Show("延迟时间不能为空！");
                return;
            }
            if (float.Parse(Delay_Time.Text) <= 0)
            {
                MessageBox.Show("延迟时间不能为负数！");
                return;
            }
            if (string.IsNullOrEmpty(Stay_Time.Text))
            {
                MessageBox.Show("保持时间不能为空！");
                return;
            }
            if (float.Parse(Stay_Time.Text) < 0)
            {
                MessageBox.Show("保持时间不能为负数！");
                return;
            }
            // 将扫描的增量转为16进制
            int incrementNumber = (int)(double.Parse(End_Position.Text) * 100) - (int)(double.Parse(Start_Position.Text) * 100);
            if (incrementNumber / 100 / float.Parse(Scan_Increment.Text) != Math.Floor(incrementNumber / 100 / float.Parse(Scan_Increment.Text)))
            {
                MessageBox.Show("扫描终点减去扫描起点必须是扫描增量的整数倍");
                return;
            }
            if ((float.Parse(Scan_Increment.Text) / 10) >= (float.Parse(Stay_Time.Text) / 1000))
            {
                MessageBox.Show("扫描增量除以10必须小于保持时间");
                return;
            }
            // 将扫描起点转为16进制
            int startNumber = (int)(double.Parse(Start_Position.Text) * 100);
                
            // 将保持时间转为16进制
            int stayNumber = (int)(double.Parse(Stay_Time.Text) * 100);
                
            // 将延迟时间转为16进制
            int delayNumber = (int)(double.Parse(Delay_Time.Text) * 100);
               
            // 将扫描增量转为16进制
            int direction = incrementNumber > 0 ? 1 : 0;
            int tinyInNumber = (int)(double.Parse(Scan_Increment.Text) * 100);
                
                
            if (Trigger_Checkbox.IsChecked == true)
            {
                // 触发勾选时
                MotorControl.Instance.Start_Interval_Trigger(Math.Abs(incrementNumber), startNumber, stayNumber, delayNumber, tinyInNumber, direction);
            } else
            {
                // 触发未勾选时
                MotorControl.Instance.Start_Interval_Untrigger(Math.Abs(incrementNumber), startNumber, stayNumber, delayNumber, tinyInNumber, direction);
            }
        }

        // 停止扫描
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MotorControl.Instance.Interrupt_Scan();
        }

        // 保存参数
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Start_Position.Text))
            {
                MessageBox.Show("扫描起点不能为空！");
                return;
            }
            if (string.IsNullOrEmpty(End_Position.Text))
            {
                MessageBox.Show("扫描终点不能为空！");
                return;
            }
            
            if (float.Parse(Start_Position.Text) == float.Parse(End_Position.Text))
            {
                MessageBox.Show("扫描起点不能和扫描终点一样！");
                return;
            }
            
            if (string.IsNullOrEmpty(Stay_Time.Text))
            {
                MessageBox.Show("保持时间不能为空！");
                return;
            }
            if (float.Parse(Stay_Time.Text) < 0)
            {
                MessageBox.Show("保持时间不能为负数！");
                return;
            }
            
            if (string.IsNullOrEmpty(Scan_Increment.Text))
            {
                MessageBox.Show("扫描增量不能为空！");
                return;
            }
            if (float.Parse(Scan_Increment.Text) <= 0)
            {
                MessageBox.Show("扫描增量不能为负数！");
                return;
            }
            if (string.IsNullOrEmpty(Delay_Time.Text))
            {
                MessageBox.Show("延迟时间不能为空！");
                return;
            }
            if (float.Parse(Delay_Time.Text) <= 0)
            {
                MessageBox.Show("延迟时间不能为负数！");
                return;
            }
            if (string.IsNullOrEmpty(Stay_Time.Text))
            {
                MessageBox.Show("保持时间不能为空！");
                return;
            }
            if (float.Parse(Stay_Time.Text) < 0)
            {
                MessageBox.Show("保持时间不能为负数！");
                return;
            }
                
            int incrementNumber = (int)(double.Parse(End_Position.Text) * 100) - (int)(double.Parse(Start_Position.Text) * 100);
            if (incrementNumber / 100 / float.Parse(Scan_Increment.Text) != Math.Floor(incrementNumber / 100 / float.Parse(Scan_Increment.Text)))
            {
                MessageBox.Show("扫描终点减去扫描起点必须是扫描增量的整数倍");
                return;
            }
            if ((float.Parse(Scan_Increment.Text) / 10) >= (float.Parse(Stay_Time.Text) / 1000))
            {
                MessageBox.Show("扫描增量除以10必须小于保持时间");
                return;
            }
            Properties.Settings.Default.intervalStart = Start_Position.Text;
            Properties.Settings.Default.intervalEnd = End_Position.Text;
            Properties.Settings.Default.intervalIncrement = Scan_Increment.Text;
            Properties.Settings.Default.intervalDelay = Delay_Time.Text;
            Properties.Settings.Default.intervalStay = Stay_Time.Text;
            Properties.Settings.Default.intervalTrigger = Trigger_Checkbox.IsChecked == true ? "1" : "0";
            Properties.Settings.Default.Save();
            MessageBox.Show("参数保存成功!");
        }

        /// <summary>
        /// 发给下位机的指令位自定义格式
        /// 第一位为03, 第二位为自定义的id 01， 第三位为操作码，操作码定义 （00为停止运动； 01为速度模式；02为模式一的未触发模式； 03为模式一的触发模式；04为模式二的未触发模式；05为模式二的触发模式；06为查询电机状态；07为速度位置模式；08初始化（无效命令先暂定）；09保存限位；0A中断扫描）
        /// </summary>
        /// 



    }
}
