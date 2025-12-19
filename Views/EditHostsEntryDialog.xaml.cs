using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DevEnv.Views
{
    public partial class EditHostsEntryDialog : Window
    {
        public string Ip { get; private set; } = string.Empty;
        public string Domain { get; private set; } = string.Empty;

        public EditHostsEntryDialog()
        {
            InitializeComponent();
            DataContext = this;
            IpTextBox.Focus();
        }

        public EditHostsEntryDialog(string ip, string domain) : this()
        {
            Ip = ip;
            Domain = domain;
            IpTextBox.Text = ip;
            DomainTextBox.Text = domain;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                Ip = IpTextBox.Text.Trim();
                Domain = DomainTextBox.Text.Trim();
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            var ip = IpTextBox.Text.Trim();
            var domain = DomainTextBox.Text.Trim();

            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("请输入IP地址", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                IpTextBox.Focus();
                return false;
            }

            if (string.IsNullOrEmpty(domain))
            {
                MessageBox.Show("请输入域名", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                DomainTextBox.Focus();
                return false;
            }

            if (!IsValidIp(ip))
            {
                MessageBox.Show("请输入有效的IP地址", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                IpTextBox.Focus();
                return false;
            }

            return true;
        }

        private bool IsValidIp(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4)
                return false;

            return parts.All(part =>
            {
                if (int.TryParse(part, out int num))
                    return num >= 0 && num <= 255;
                return false;
            });
        }
    }
}
