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
    /// CustomMessageBox.xaml 的交互逻辑
    /// </summary>
    public partial class CustomMessageBox : UserControl
    {
       
        public CustomMessageBox()
        {
            InitializeComponent();
        }

        private void CoiledTrigger(object sender, RoutedEventArgs e)
        {
            ModeSelectDialog dialog = new ModeSelectDialog(1);
            Window popupWindow = new Window
            {
                Content = dialog,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize, // 不允许resize
                WindowStartupLocation = WindowStartupLocation.CenterOwner, // 出现在当前窗体的中心
                Title = "连续模式" // 设置弹窗的标题
            };

            popupWindow.ShowDialog();
        }

        private void IntervalTrigger(object sender, RoutedEventArgs e)
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
    }
}
