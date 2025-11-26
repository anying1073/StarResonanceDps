using System.Windows;
using System.Windows.Input;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views
{
    /// <summary>
    /// PersonalDpsView.xaml 的交互逻辑
    /// </summary>
    public partial class PersonalDpsView : Window
    {
        public PersonalDpsView(PersonalDpsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void ViewBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                Window.GetWindow(this)?.DragMove();
        }
    }
}
