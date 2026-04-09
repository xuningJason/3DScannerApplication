using System.Text;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;

namespace _3DScannerApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 上位机向下位机（esp32板子）发送指令的时候将ID设置为1，以跟下位机发送给电机指令区分开（电机ID为2）
        private System.Timers.Timer timer;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 发给下位机的指令位自定义格式
        /// 第一位为03, 第二位为自定义的id 01， 第三位为操作码，操作码定义 （00为停止运动； 01为速度模式；02为模式一的未触发模式； 03为模式一的触发模式；04为模式二的未触发模式；05为模式二的触发模式；06位查询电机状态；07速度位置模式；08初始化电机<向前走一小段>）
        /// </summary>



        // 打开弹框
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ModeSelectDialog dialog = new ModeSelectDialog(2);
            Window popupWindow = new Window
            {
                Content = dialog,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize, // 不允许resize
                WindowStartupLocation = WindowStartupLocation.CenterOwner, // 出现在当前窗体的中心
                Title = "间隔模式" // 设置弹窗的标题
            };

            popupWindow.ShowDialog();
        }

        

        // 连接串口
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (Conect_Button.Content.ToString() == "连接")
            {
                MotorControl.Instance.CreateMotorControl();
                

                Thread.Sleep(1500);

                if (!MotorControl.Instance.isConnected)
                {
                    MessageBox.Show("连接失败，请检查网络");

                    MotorControl.Instance.SerialClose();
                    return;
                }
                
                
                try
                {
                    
                    MotorControl.Instance.isReadStatus = true;
                    MotorControl.Instance.uartSendCount = 0;
                    Angle_Text.IsEnabled = true;
                    Target_Btn.IsEnabled = true;
                    Limit_Small_Text.IsEnabled = true;
                    Save_Limit_Btn.IsEnabled = true;
                    Mode_Select_Btn.IsEnabled = true;
                    Laser_Switch.IsEnabled = true;

                    // 创建定时器，每隔一秒触发一次
                    timer = new System.Timers.Timer(500);
                    timer.Elapsed += Timer_Elapsed;
                    timer.AutoReset = true; // 自动重置

                    // 启动定时器
                    timer.Start();
                    Connection_Led.Color = Colors.LightGreen;
                    Conect_Button.Content = "断开";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"通信错误: {ex.Message}");
                }
            }
            else
            {
                MotorControl.Instance.isReadStatus = false;
                MotorControl.Instance.SerialClose();
                MotorControl.Instance.uartSendCount = 0;
                //Check_Button.IsEnabled = false;
                Angle_Text.IsEnabled = false;
                Target_Btn.IsEnabled = false;
                Mode_Select_Btn.IsEnabled = false;
                Laser_Switch.IsEnabled = false;
                Connection_Led.Color = Colors.Gray;
                Limit_Small_Text.IsEnabled = false;
                Save_Limit_Btn.IsEnabled = false;
                Conect_Button.Content = "连接";
                Laser_Switch.Content = "打开激光";
            }
           
        }


        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                float tem = 0.0f;
                tem = MotorControl.Instance.tempretureFlo >= 32768 ? (MotorControl.Instance.tempretureFlo - 65535) / 256 : MotorControl.Instance.tempretureFlo / 256;
                //SpeedText.Text = $"读取的速度值 (时间: {DateTime.Now})：{speedFlo}°/s";
                PositionText.Text = $"{MotorControl.Instance.positionFlo.ToString("0.000")}°";
                TempretureText.Text = $"{tem.ToString("0.00")}℃";
                PresureText.Text = $"{MotorControl.Instance.presureFlo.ToString("0.00")}Bar";
            });
        }

        


       

        #region 运动方法


        private void Go_Target(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Angle_Text.Text))
            {
                MessageBox.Show("目标位置不能为空");
                return;
            }
            
            if (float.Parse(Angle_Text.Text) < -20 || float.Parse(Angle_Text.Text) > 20)
            {
                MessageBox.Show("目标位置必须在-20~20之间");
                return;
            }
            int speed = 0;
            float angle = float.Parse(Angle_Text.Text);
            MotorControl.Instance.Motor_To_Target(speed, angle);
        }

        #endregion

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                timer?.Stop();
                System.Environment.Exit(0);
            }
            catch (Exception) 
            { 
            }
        }

        private void Switch_Laser(object sender, RoutedEventArgs e)
        {
            if (Laser_Switch.Content.ToString() == "打开激光")
            {
                MotorControl.Instance.OpenLaser();
                Laser_Switch.Content = "关闭激光";
            } else
            {
                MotorControl.Instance.CloseLaser();
                Laser_Switch.Content = "打开激光";
            }
        }

        private void Save_Limit(object sender, RoutedEventArgs e)
        {
            int brightness;
            bool isSuc = int.TryParse(Limit_Small_Text.Text, out brightness);
            if (isSuc)
            {
                if (brightness < 0 || brightness > 100)
                {
                    MessageBox.Show("请输入0~100的数字");
                    return;
                }
                MotorControl.Instance.Save_Limit_To_ESP(brightness);
            } else
            {
                MessageBox.Show("请输入正确的数字");
            }
        }
    }
}