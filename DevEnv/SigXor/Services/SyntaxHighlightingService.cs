using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using DevEnv.Host.Themes;
using ICSharpCode.AvalonEdit.Highlighting;

namespace DevEnv.Host.SigXor.Services;

/// <summary>
/// 注册自定义语法高亮定义（JSON、TOML、YAML、Log）。
/// 使用纯代码创建高亮定义，不依赖 XSHD 文件解析。
/// 高亮配色跟随应用主题：深色用 VS Code Dark，浅色用 VS Code Light，
/// 主题切换时重新注册定义，已打开的文本编辑器会重新取一次定义。
/// </summary>
public static class SyntaxHighlightingService
{
    private static bool _initialized;
    private static bool _dark;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _dark = ThemeManager.IsDark;
        ThemeManager.ThemeChanged += OnThemeChanged;
        RegisterAll();
    }

    private static void OnThemeChanged(bool dark)
    {
        if (!_initialized || dark == _dark) return;
        _dark = dark;
        RegisterAll();
    }

    private static void RegisterAll()
    {
        try
        {
            RegisterJson();
            RegisterToml();
            RegisterYaml();
            RegisterLog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register syntax highlightings: {ex}");
        }
    }

    #region 辅助方法

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>按当前主题取配色：深色 / 浅色分别为 VS Code Dark / Light。</summary>
    private static Color C(string darkHex, string lightHex) => Hex(_dark ? darkHex : lightHex);

    private static HighlightingColor Color(string name, Color fg, bool bold = false, bool italic = false)
    {
        var c = new HighlightingColor { Name = name };
        c.Foreground = new SimpleHighlightingBrush(fg);
        if (bold) c.FontWeight = FontWeights.Bold;
        if (italic) c.FontStyle = FontStyles.Italic;
        return c;
    }

    private static HighlightingRule Rule(string name, Color fg, string pattern,
        bool multiline = false, bool bold = false, bool italic = false)
    {
        var opts = RegexOptions.Compiled;
        if (multiline) opts |= RegexOptions.Multiline;
        return new HighlightingRule
        {
            Color = Color(name, fg, bold, italic),
            Regex = new Regex(pattern, opts)
        };
    }

    /// <summary>创建字符串 Span（必须有 begin 和 end）。</summary>
    private static HighlightingSpan StringSpan(string begin, string end, Color fg, bool multiline = false)
    {
        var opts = RegexOptions.Compiled;
        return new HighlightingSpan
        {
            StartExpression = new Regex(Regex.Escape(begin), opts),
            EndExpression = new Regex(Regex.Escape(end), opts),
            SpanColor = Color("String", fg),
            RuleSet = new HighlightingRuleSet(),
            SpanColorIncludesStart = true,
            SpanColorIncludesEnd = false
        };
    }

    private static void RegisterDef(string name, string[] extensions, HighlightingRuleSet ruleSet)
    {
        var def = new MyHighlightingDefinition(name, extensions, ruleSet);
        HighlightingManager.Instance.RegisterHighlighting(name, extensions, def);
        System.Diagnostics.Debug.WriteLine($"Registered syntax highlighting: {name} [{string.Join(", ", extensions)}]");
    }

    #endregion

    #region JSON

    private static void RegisterJson()
    {
        var rs = new HighlightingRuleSet();

        // 字符串
        rs.Spans.Add(StringSpan("\"", "\"", C("#CE9178", "#A31515")));

        // 数字
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?"));
        // 布尔值
        rs.Rules.Add(Rule("Boolean", C("#569CD6", "#0000FF"), @"\b(?:true|false)\b"));
        // null
        rs.Rules.Add(Rule("Null", C("#569CD6", "#0000FF"), @"\bnull\b"));
        // 标点
        rs.Rules.Add(Rule("Punctuation", C("#D4D4D4", "#333333"), @"[{}\[\],;:]"));

        RegisterDef("JSON", [".json"], rs);
    }

    #endregion

    #region TOML

    private static void RegisterToml()
    {
        var rs = new HighlightingRuleSet();

        // 字符串（Span 优先匹配，# 在字符串内不会被当作注释）
        rs.Spans.Add(StringSpan("\"\"\"", "\"\"\"", C("#CE9178", "#A31515"), multiline: true));
        rs.Spans.Add(StringSpan("'''", "'''", C("#CE9178", "#A31515"), multiline: true));
        rs.Spans.Add(StringSpan("\"", "\"", C("#CE9178", "#A31515")));
        rs.Spans.Add(StringSpan("'", "'", C("#CE9178", "#A31515")));

        // 表名
        rs.Rules.Add(Rule("TableName", C("#4EC9B0", "#267F99"), @"^\s*\[\[[\w\.\-]+\]\]", multiline: true, bold: true));
        rs.Rules.Add(Rule("TableName", C("#4EC9B0", "#267F99"), @"^\s*\[[\w\.\-]+\]", multiline: true, bold: true));

        // 日期时间
        rs.Rules.Add(Rule("DateTime", C("#4EC9B0", "#267F99"),
            @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?"));

        // 数字
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0x[0-9a-fA-F_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0o[0-7_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0b[01_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"[+-]?(?:inf|nan)"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"),
            @"[+-]?\d[0-9_]*(?:\.\d[0-9_]*)?(?:[eE][+-]?\d[0-9_]*)?"));

        // 布尔值
        rs.Rules.Add(Rule("Boolean", C("#569CD6", "#0000FF"), @"\b(?:true|false)\b"));

        // 键
        rs.Rules.Add(Rule("Key", C("#9CDCFE", "#0451A5"), @"[a-zA-Z_][\w\-\.]*(?=\s*=)"));

        // 注释（放在最后，Span 优先级高于 Rule，字符串内的 # 已被 Span 消费）
        rs.Rules.Add(Rule("Comment", C("#6A9955", "#008000"), @"#[^\n]*", italic: true));

        RegisterDef("TOML", [".toml"], rs);
    }

    #endregion

    #region YAML

    private static void RegisterYaml()
    {
        var rs = new HighlightingRuleSet();

        // 字符串
        rs.Spans.Add(StringSpan("\"\"\"", "\"\"\"", C("#CE9178", "#A31515"), multiline: true));
        rs.Spans.Add(StringSpan("'''", "'''", C("#CE9178", "#A31515"), multiline: true));
        rs.Spans.Add(StringSpan("\"", "\"", C("#CE9178", "#A31515")));
        rs.Spans.Add(StringSpan("'", "'", C("#CE9178", "#A31515")));

        // 文档分隔符
        rs.Rules.Add(Rule("DocumentSeparator", C("#808080", "#7A7A7A"), @"^---\s*$", multiline: true, bold: true));
        rs.Rules.Add(Rule("DocumentSeparator", C("#808080", "#7A7A7A"), @"^\.\.\.\s*$", multiline: true, bold: true));

        // 标签
        rs.Rules.Add(Rule("Tag", C("#C586C0", "#800000"), @"![\w]+(?:\.[\w]+)*"));
        // 锚点 & 别名
        rs.Rules.Add(Rule("Anchor", C("#DCDCAA", "#795E26"), @"&[\w\-]+"));
        rs.Rules.Add(Rule("Alias", C("#DCDCAA", "#795E26"), @"\*[\w\-]+"));

        // 日期时间
        rs.Rules.Add(Rule("Date", C("#4EC9B0", "#267F99"),
            @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?"));
        rs.Rules.Add(Rule("Date", C("#4EC9B0", "#267F99"), @"\d{4}-\d{2}-\d{2}"));

        // 数字
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0x[0-9a-fA-F_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0o[0-7_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"0b[01_]+"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"[+-]?\.(?:inf|Inf|INF)"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"), @"[+-]?\.(?:nan|NaN|NAN)"));
        rs.Rules.Add(Rule("Number", C("#B5CEA8", "#098658"),
            @"[+-]?\d[0-9_]*(?:\.\d[0-9_]*)?(?:[eE][+-]?\d[0-9_]*)?"));

        // 布尔值
        rs.Rules.Add(Rule("Boolean", C("#569CD6", "#0000FF"),
            @"\b(?:true|false|True|False|TRUE|FALSE|yes|no|Yes|No|YES|NO|on|off|On|Off|ON|OFF)\b"));
        // Null
        rs.Rules.Add(Rule("Null", C("#569CD6", "#0000FF"), @"\b(?:null|Null|NULL)\b"));

        // 键
        rs.Rules.Add(Rule("Key", C("#9CDCFE", "#0451A5"), @"^\s*[\w][\w\-\.]*(?=\s*:)", multiline: true));

        // 注释（最后，Span 优先级高于 Rule）
        rs.Rules.Add(Rule("Comment", C("#6A9955", "#008000"), @"#[^\n]*", italic: true));

        RegisterDef("YAML", [".yaml", ".yml"], rs);
    }

    #endregion

    #region Log

    private static void RegisterLog()
    {
        var rs = new HighlightingRuleSet();

        // 时间戳
        rs.Rules.Add(Rule("Timestamp", C("#90CAF9", "#0451A5"),
            @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b"));
        rs.Rules.Add(Rule("Timestamp", C("#90CAF9", "#0451A5"),
            @"\b\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?\b"));

        // 日志级别
        rs.Rules.Add(Rule("Error", C("#EF9A9A", "#CD3131"), @"\b(?:ERROR|FATAL|CRITICAL)\b", bold: true));
        rs.Rules.Add(Rule("Warning", C("#FFE082", "#B58900"), @"\bWARN(?:ING)?\b"));
        rs.Rules.Add(Rule("Info", C("#A5D6A7", "#22863A"), @"\bINFO\b"));
        rs.Rules.Add(Rule("Debug", C("#B0BEC5", "#6A737D"), @"\bDEBUG\b"));
        rs.Rules.Add(Rule("Trace", C("#B0BEC5", "#6A737D"), @"\b(?:TRACE|VERBOSE)\b", italic: true));

        // 异常
        rs.Rules.Add(Rule("Exception", C("#EF9A9A", "#CD3131"), @"\b\w+Exception\b", italic: true));

        // 堆栈
        rs.Rules.Add(Rule("StackTrace", C("#B0BEC5", "#6A737D"), @"^\s+at\s+.*$", multiline: true));

        RegisterDef("Log", [".log"], rs);
    }

    #endregion
}

/// <summary>自定义高亮定义实现。</summary>
file sealed class MyHighlightingDefinition : IHighlightingDefinition
{
    public MyHighlightingDefinition(string name, string[] extensions, HighlightingRuleSet mainRuleSet)
    {
        Name = name;
        Extensions = extensions;
        MainRuleSet = mainRuleSet;
    }

    public string Name { get; }
    public string[] Extensions { get; }
    public HighlightingRuleSet MainRuleSet { get; }
    public IEnumerable<HighlightingColor> NamedHighlightingColors { get; } = [];
    public IDictionary<string, string> Properties => new Dictionary<string, string>();

    public HighlightingColor? GetNamedColor(string name) => null;
    public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
}
