using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevEnv.Core.Codec;
using DevEnv.Core;
using DevEnv.Host.Infrastructure;

namespace DevEnv.Host.ViewModels;

public enum CodecToolKind
{
    Base64,
    Url,
    Hex,
    Unicode,
    Html,
    Aes,
    Xor,
    Hash,
    Time,
}

/// <summary>编解码工具箱：菜单打开可关闭页签，页内左右输入输出。</summary>
public sealed partial class CodecToolViewModel : ObservableObject
{
    private const string PageId = "codec";
    private int _tabSeq;

    public CodecToolViewModel()
    {
        Tabs = [];
        OpenTab(CodecToolKind.Base64);
    }

    public ObservableCollection<CodecTabViewModel> Tabs { get; }

    [ObservableProperty]
    private CodecTabViewModel? _selectedTab;

    public bool HasTabs => Tabs.Count > 0;

    [RelayCommand]
    private void OpenBase64() => OpenTab(CodecToolKind.Base64);

    [RelayCommand]
    private void OpenUrl() => OpenTab(CodecToolKind.Url);

    [RelayCommand]
    private void OpenHex() => OpenTab(CodecToolKind.Hex);

    [RelayCommand]
    private void OpenUnicode() => OpenTab(CodecToolKind.Unicode);

    [RelayCommand]
    private void OpenHtml() => OpenTab(CodecToolKind.Html);

    [RelayCommand]
    private void OpenAes() => OpenTab(CodecToolKind.Aes);

    [RelayCommand]
    private void OpenXor() => OpenTab(CodecToolKind.Xor);

    [RelayCommand]
    private void OpenHash() => OpenTab(CodecToolKind.Hash);

    [RelayCommand]
    private void OpenTime() => OpenTab(CodecToolKind.Time);

    public void OpenTab(CodecToolKind kind)
    {
        var existing = Tabs.FirstOrDefault(t => t.Kind == kind);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var tab = new CodecTabViewModel(kind, ++_tabSeq, Report);
        Tabs.Add(tab);
        SelectedTab = tab;
        OnPropertyChanged(nameof(HasTabs));
    }

    [RelayCommand]
    private void CloseTab(CodecTabViewModel? tab)
    {
        if (tab is null)
            return;

        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;

        Tabs.RemoveAt(index);
        SelectedTab = Tabs.Count == 0
            ? null
            : Tabs[Math.Clamp(index, 0, Tabs.Count - 1)];
        OnPropertyChanged(nameof(HasTabs));
    }

    [RelayCommand]
    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            HostServices.DialogOwner,
            "编码工具箱\n\n"
            + "· 编码 / 解码：Base64、URL、Hex、Unicode、HTML\n"
            + "· 加密 / 解密：AES-256-CBC、XOR\n"
            + "· 哈希：MD5 / SHA1 / SHA256 / SHA384 / SHA512\n"
            + "· 时间：Unix 时间戳与本地时间互转\n\n"
            + "AES 密文格式为 Base64(IV + Cipher)，密钥经 SHA256 派生。",
            "关于",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Report(string message) => HostServices.SetStatus(message, PageId);
}

/// <summary>单个工具页签的状态与操作。</summary>
public sealed partial class CodecTabViewModel : ObservableObject
{
    private readonly Action<string> _report;

    public CodecTabViewModel(CodecToolKind kind, int id, Action<string> report)
    {
        Kind = kind;
        Id = id;
        _report = report;
        Title = TitleOf(kind);
        TextEncoding = "UTF-8";
        HashAlgorithm = "SHA256";
        UseMilliseconds = false;
        ModeIndex = 0;
    }

    public CodecToolKind Kind { get; }
    public int Id { get; }
    public string Title { get; }

    public IReadOnlyList<string> TextEncodings => CodecToolkit.TextEncodings;
    public IReadOnlyList<string> HashAlgorithms => CodecToolkit.HashAlgorithms;

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    private string _output = string.Empty;

    [ObservableProperty]
    private string _textEncoding = "UTF-8";

    [ObservableProperty]
    private string _hashAlgorithm = "SHA256";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _useMilliseconds;

    /// <summary>0=文本 1=图片 2=文件（Base64 专用）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextMode))]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    private int _modeIndex;

    public bool IsTextMode => ModeIndex == 0;
    public bool IsFileMode => ModeIndex is 1 or 2;

    public bool ShowsEncoding => Kind is CodecToolKind.Base64 or CodecToolKind.Hex
        or CodecToolKind.Aes or CodecToolKind.Xor or CodecToolKind.Hash;

    public bool ShowsPassword => Kind is CodecToolKind.Aes or CodecToolKind.Xor;
    public bool ShowsHashAlgorithm => Kind == CodecToolKind.Hash;
    public bool ShowsTimeOptions => Kind == CodecToolKind.Time;
    public bool ShowsBase64Modes => Kind == CodecToolKind.Base64;
    public bool ShowsPrimaryPair => Kind != CodecToolKind.Hash && Kind != CodecToolKind.Time;
    public bool ShowsHashActions => Kind == CodecToolKind.Hash;
    public bool ShowsTimeActions => Kind == CodecToolKind.Time;

    public string InputHint => Kind switch
    {
        CodecToolKind.Base64 => "请输入要编码的文本，或要解码的 Base64 字符串…",
        CodecToolKind.Url => "请输入要 URL 编码 / 解码的文本…",
        CodecToolKind.Hex => "请输入文本，或十六进制字符串…",
        CodecToolKind.Unicode => "请输入文本，或 \\uXXXX 序列…",
        CodecToolKind.Html => "请输入文本，或 HTML 实体…",
        CodecToolKind.Aes => "请输入明文，或 Base64 密文…",
        CodecToolKind.Xor => "请输入明文；解密时输入十六进制密文…",
        CodecToolKind.Hash => "请输入要计算哈希的文本…",
        CodecToolKind.Time => "请输入 Unix 时间戳，或可解析的时间字符串…",
        _ => string.Empty,
    };

    public string OutputHint => Kind switch
    {
        CodecToolKind.Hash => "哈希结果将显示在这里…",
        CodecToolKind.Time => "转换结果将显示在这里…",
        _ => "编码 / 解码结果将显示在这里…",
    };

    public string PrimaryLabel => Kind switch
    {
        CodecToolKind.Aes or CodecToolKind.Xor => "加密 ↓",
        _ => "编码 ↓",
    };

    public string SecondaryLabel => Kind switch
    {
        CodecToolKind.Aes or CodecToolKind.Xor => "解密 ↓",
        _ => "解码 ↓",
    };

    [RelayCommand]
    private void RunPrimary()
    {
        if (Kind == CodecToolKind.Base64 && IsFileMode)
        {
            EncodeFile();
            return;
        }

        var result = Kind switch
        {
            CodecToolKind.Base64 => CodecToolkit.Base64Encode(Input, TextEncoding),
            CodecToolKind.Url => CodecToolkit.UrlEncode(Input),
            CodecToolKind.Hex => CodecToolkit.HexEncode(Input, TextEncoding),
            CodecToolKind.Unicode => CodecToolkit.UnicodeEncode(Input),
            CodecToolKind.Html => CodecToolkit.HtmlEncode(Input),
            CodecToolKind.Aes => CodecToolkit.AesEncrypt(Input, Password, TextEncoding),
            CodecToolKind.Xor => CodecToolkit.XorCrypt(Input, Password, TextEncoding),
            _ => CodecResult.Fail("当前工具不支持此操作"),
        };
        Apply(result, "完成");
    }

    [RelayCommand]
    private void RunSecondary()
    {
        if (Kind == CodecToolKind.Base64 && IsFileMode)
        {
            DecodeFile();
            return;
        }

        var result = Kind switch
        {
            CodecToolKind.Base64 => CodecToolkit.Base64Decode(Input, TextEncoding),
            CodecToolKind.Url => CodecToolkit.UrlDecode(Input),
            CodecToolKind.Hex => CodecToolkit.HexDecode(Input, TextEncoding),
            CodecToolKind.Unicode => CodecToolkit.UnicodeDecode(Input),
            CodecToolKind.Html => CodecToolkit.HtmlDecode(Input),
            CodecToolKind.Aes => CodecToolkit.AesDecrypt(Input, Password, TextEncoding),
            CodecToolKind.Xor => CodecToolkit.XorDecryptHex(Input, Password, TextEncoding),
            _ => CodecResult.Fail("当前工具不支持此操作"),
        };
        Apply(result, "完成");
    }

    [RelayCommand]
    private void RunHash()
    {
        var result = CodecToolkit.HashText(Input, HashAlgorithm, TextEncoding);
        Apply(result, $"{HashAlgorithm} 完成");
    }

    [RelayCommand]
    private void TimeToDate()
    {
        var result = CodecToolkit.UnixToDateTime(Input, UseMilliseconds);
        Apply(result, "已转换为时间");
    }

    [RelayCommand]
    private void TimeToUnix()
    {
        var result = CodecToolkit.DateTimeToUnix(Input, UseMilliseconds);
        Apply(result, "已转换为时间戳");
    }

    [RelayCommand]
    private void TimeNow()
    {
        var result = CodecToolkit.NowTimestamps();
        if (result.Ok)
        {
            Output = result.Text;
            Input = result.Text.Split('\n')
                .FirstOrDefault(l => l.StartsWith(UseMilliseconds ? "毫秒：" : "秒：", StringComparison.Ordinal))
                ?.Split('：')
                .ElementAtOrDefault(1)
                ?.Trim() ?? Input;
        }
        Apply(result, "已填入当前时间");
    }

    [RelayCommand]
    private void Swap()
    {
        (Input, Output) = (Output, Input);
        _report("已交换输入 / 输出");
    }

    [RelayCommand]
    private void Clear()
    {
        Input = string.Empty;
        Output = string.Empty;
        _report("已清空");
    }

    [RelayCommand]
    private void CopyInput()
    {
        if (string.IsNullOrEmpty(Input))
        {
            _report("输入为空");
            return;
        }
        HostCallbacks.SetClipboardText?.Invoke(Input);
        _report("已复制输入");
    }

    [RelayCommand]
    private void CopyOutput()
    {
        if (string.IsNullOrEmpty(Output))
        {
            _report("输出为空");
            return;
        }
        HostCallbacks.SetClipboardText?.Invoke(Output);
        _report("已复制输出");
    }

    [RelayCommand]
    private void PickFile()
    {
        var filter = ModeIndex == 1
            ? "图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有文件|*.*"
            : "所有文件|*.*";
        var files = HostCallbacks.PickFiles?.Invoke(filter);
        if (files is not { Length: > 0 })
            return;

        try
        {
            var bytes = File.ReadAllBytes(files[0]);
            var result = CodecToolkit.Base64EncodeBytes(bytes);
            if (!result.Ok)
            {
                _report(result.Error);
                return;
            }

            Input = result.Text;
            _report($"已读入 {Path.GetFileName(files[0])}（{bytes.Length} 字节）");
        }
        catch (Exception ex)
        {
            _report(ex.Message);
        }
    }

    private void EncodeFile()
    {
        if (string.IsNullOrWhiteSpace(Input) && IsFileMode)
        {
            PickFile();
            if (string.IsNullOrWhiteSpace(Input))
                return;
        }

        // 文件模式：输入已是 Base64，编码即把路径读入；若输入像路径则读文件
        if (File.Exists(Input.Trim()))
        {
            try
            {
                var bytes = File.ReadAllBytes(Input.Trim());
                Apply(CodecToolkit.Base64EncodeBytes(bytes), "文件已编码");
            }
            catch (Exception ex)
            {
                _report(ex.Message);
            }
            return;
        }

        Apply(CodecToolkit.Base64Encode(Input, TextEncoding), "完成");
    }

    private void DecodeFile()
    {
        var decoded = CodecToolkit.Base64DecodeToBytes(Input);
        if (!decoded.Ok || decoded.Bytes is null)
        {
            _report(decoded.Error);
            return;
        }

        var path = HostCallbacks.SaveFile?.Invoke(
            ModeIndex == 1 ? "image.png" : "decoded.bin",
            ModeIndex == 1 ? "图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*" : "所有文件|*.*");
        if (string.IsNullOrWhiteSpace(path))
        {
            Output = decoded.Text;
            _report("已解码为 Base64 预览（未保存文件）");
            return;
        }

        try
        {
            File.WriteAllBytes(path, decoded.Bytes);
            Output = path;
            _report($"已保存到 {path}");
        }
        catch (Exception ex)
        {
            _report(ex.Message);
        }
    }

    private void Apply(CodecResult result, string okMessage)
    {
        if (!result.Ok)
        {
            _report(result.Error);
            return;
        }

        Output = result.Text;
        _report(okMessage);
    }

    private static string TitleOf(CodecToolKind kind) => kind switch
    {
        CodecToolKind.Base64 => "Base64 编解码",
        CodecToolKind.Url => "URL 编解码",
        CodecToolKind.Hex => "Hex 编解码",
        CodecToolKind.Unicode => "Unicode 编解码",
        CodecToolKind.Html => "HTML 编解码",
        CodecToolKind.Aes => "AES 加解密",
        CodecToolKind.Xor => "XOR 加解密",
        CodecToolKind.Hash => "哈希计算",
        CodecToolKind.Time => "时间工具",
        _ => "工具",
    };
}
