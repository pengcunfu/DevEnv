using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DevEnv.ViewModels;

namespace DevEnv.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
        }

        private async void StartService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ServiceItemViewModel service)
            {
                await service.StartServiceAsync();
            }
        }

        private async void StopService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ServiceItemViewModel service)
            {
                await service.StopServiceAsync();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.StopMonitoring();
            base.OnClosed(e);
        }
    }
}