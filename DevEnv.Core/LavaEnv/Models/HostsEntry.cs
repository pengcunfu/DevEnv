using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DevEnv.Core.LavaEnv.Models;

/// <summary>Hosts 按条编辑中的一行：地址 + 一个或多个主机名。</summary>
public sealed class HostsEntry : INotifyPropertyChanged
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private string _ip = string.Empty;
    private string _domain = string.Empty;
    private int _lineNumber;
    private string _originalLine = string.Empty;

    public string Ip
    {
        get => _ip;
        set
        {
            if (_ip == value)
                return;
            _ip = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>主机名，多个时以空格分隔。</summary>
    public string Domain
    {
        get => _domain;
        set
        {
            if (_domain == value)
                return;
            _domain = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>原文中的行号（从 1 起）；新增条目为 0。</summary>
    public int LineNumber
    {
        get => _lineNumber;
        set
        {
            if (_lineNumber == value)
                return;
            _lineNumber = value;
            OnPropertyChanged();
        }
    }

    public string OriginalLine
    {
        get => _originalLine;
        set
        {
            if (_originalLine == value)
                return;
            _originalLine = value;
            OnPropertyChanged();
        }
    }

    public string DisplayText => $"{Ip}\t{Domain}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public static bool TryParse(string line, [NotNullWhen(true)] out HostsEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (trimmed.StartsWith('#'))
            return false;

        var hash = trimmed.IndexOf('#');
        var content = (hash >= 0 ? trimmed[..hash] : trimmed).Trim();
        if (content.Length == 0)
            return false;

        var parts = Whitespace.Split(content);
        if (parts.Length < 2)
            return false;

        if (!TryParseAddress(parts[0], out _))
            return false;

        entry = new HostsEntry
        {
            Ip = parts[0],
            Domain = string.Join(' ', parts.Skip(1)),
            OriginalLine = content,
        };
        return true;
    }

    /// <summary>
    /// IPv6 走 <see cref="IPAddress.TryParse"/>；IPv4 强制四段十进制，
    /// 避免接受 <c>1.2.3</c> 这类简写。
    /// </summary>
    public static bool TryParseAddress(string? value, out IPAddress address)
    {
        address = IPAddress.None;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        if (value.Contains(':'))
        {
            if (!IPAddress.TryParse(value, out var parsed) || parsed is null)
                return false;

            address = parsed;
            return parsed.AddressFamily == AddressFamily.InterNetworkV6;
        }

        var octets = value.Split('.');
        if (octets.Length != 4)
            return false;

        foreach (var part in octets)
        {
            if (part.Length is 0 or > 3 || !part.All(char.IsAsciiDigit))
                return false;
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return false;
        }

        if (!IPAddress.TryParse(value, out var ipv4) || ipv4 is null)
            return false;

        address = ipv4;
        return true;
    }
}
