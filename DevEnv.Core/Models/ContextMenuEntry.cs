using Newtonsoft.Json;

namespace DevEnv.Core.Models;

/// <summary>
/// 右键菜单项配置（对应 menu_configs.json 里的条目）。
/// 字段与 JSON 一一对应，两个程序共用同一份配置文件。
/// </summary>
public sealed class ContextMenuEntry
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("root")]
    public string Root { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("for_files")]
    public bool ForFiles { get; set; }

    [JsonProperty("for_directories")]
    public bool ForDirectories { get; set; }

    [JsonProperty("for_desktop")]
    public bool ForDesktop { get; set; }

    /// <summary>作用域的可读描述，用于界面展示。</summary>
    [JsonIgnore]
    public string ScopeText
    {
        get
        {
            var parts = new List<string>();
            if (ForFiles) parts.Add("文件");
            if (ForDirectories) parts.Add("目录");
            if (ForDesktop) parts.Add("桌面");
            return parts.Count == 0 ? "未设置" : string.Join(" + ", parts);
        }
    }
}

/// <summary>配置文件根对象。</summary>
internal sealed class ContextMenuConfigFile
{
    [JsonProperty("configs")]
    public List<ContextMenuEntry> Configs { get; set; } = [];
}