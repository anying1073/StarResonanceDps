using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views
{
    /// <summary>
    /// PersonalDpsView.xaml 的交互逻辑
    /// </summary>
    public partial class PersonalDpsView : Window
    {
        public PersonalDpsView(PersonalDpsViewModel viewModel, IConfigManager configManager)
        {
            DataContext = viewModel;
            InitializeComponent();
     
            // ⭐ 修改: 将界面等比例缩小200像素(缩放比例约0.714,相当于从700缩小到500)
            const double scale = 0.714285714;  // 500/700 = 0.714...
 
            // 设置窗口尺寸为缩放后的尺寸
            Width = 700 * scale;  // 原700 -> 约500
            Height = 96 * scale;  // 原96 -> 约68.6
            
            // ⭐ 关键修改: 对窗口内容应用LayoutTransform缩放
            // 这样可以让XAML中的所有固定尺寸(按钮、图标、文字等)都按比例缩小
            if (Content is FrameworkElement rootElement)
            {
                rootElement.LayoutTransform = new ScaleTransform(scale, scale);
            }
            
            // 从配置中读取置顶状态,与DPS统计窗口保持一致
            Topmost = configManager.CurrentConfig.TopmostEnabled;
        }

        private void ViewBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                Window.GetWindow(this)?.DragMove();
        }
    }
}
