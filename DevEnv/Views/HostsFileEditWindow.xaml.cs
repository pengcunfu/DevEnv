using Avalonia.Controls;
using DevEnv.ViewModels;

namespace DevEnv.Views
{
    public partial class HostsFileEditWindow : Window
    {
        private readonly HostsFileEditViewModel _viewModel;

        public HostsFileEditWindow()
        {
            InitializeComponent();
            _viewModel = new HostsFileEditViewModel();
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}

