using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DevEnv.Models
{
    public class HostsEntry : INotifyPropertyChanged
    {
        private string _ip = string.Empty;
        private string _domain = string.Empty;
        private int _lineNumber;
        private string _originalLine = string.Empty;

        public string Ip
        {
            get => _ip;
            set
            {
                if (_ip != value)
                {
                    _ip = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Domain
        {
            get => _domain;
            set
            {
                if (_domain != value)
                {
                    _domain = value;
                    OnPropertyChanged();
                }
            }
        }

        public int LineNumber
        {
            get => _lineNumber;
            set
            {
                if (_lineNumber != value)
                {
                    _lineNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OriginalLine
        {
            get => _originalLine;
            set
            {
                if (_originalLine != value)
                {
                    _originalLine = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayText => $"{Ip}\t{Domain}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static bool TryParse(string line, out HostsEntry? entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                return false;

            // 匹配IP和域名的正则表达式
            var match = Regex.Match(line.Trim(), @"^(\S+)\s+(\S+)");
            if (match.Success)
            {
                var ip = match.Groups[1].Value;
                var domain = match.Groups[2].Value;

                if (IsValidIp(ip))
                {
                    entry = new HostsEntry
                    {
                        Ip = ip,
                        Domain = domain,
                        OriginalLine = line.Trim()
                    };
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidIp(string ip)
        {
            // 简单的IP地址验证
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
