using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevBox.Api;

/// <summary>读写 data.json 中的快捷方式列表。</summary>
public sealed class DataStore
{
    private readonly object _gate = new();

    public JArray GetList(string propertyName)
    {
        lock (_gate)
            return (JArray?)LoadRoot()[propertyName] ?? new JArray();
    }

    public JArray SaveList(string propertyName, JArray list)
    {
        lock (_gate)
        {
            var root = LoadRoot();
            root[propertyName] = list;
            SaveRoot(root);
            return list;
        }
    }

    public JObject LoadRoot()
    {
        if (!File.Exists(AppPaths.DataJsonPath))
        {
            var fresh = new JObject
            {
                ["Config"] = new JObject
                {
                    ["TrayIcon"] = true,
                    ["SelfStarting"] = false,
                    ["BaseEnvPath"] = @"D:\ProgramBox\env",
                    ["Language"] = "zh-CN",
                    ["DarkMode"] = false,
                    ["DoubleClickToLaunch"] = false,
                    ["ShowWindowHotkey"] = "Ctrl+Alt+D",
                },
                ["NativeAtomList"] = new JArray(),
                ["FolderAtomList"] = new JArray(),
                ["WebAtomList"] = new JArray(),
                ["WinToolAtomList"] = new JArray(),
                ["InsAtomList"] = new JArray(),
            };
            SaveRoot(fresh);
            return fresh;
        }

        var json = File.ReadAllText(AppPaths.DataJsonPath);
        return JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }

    public void SaveRoot(JObject root)
    {
        var dir = Path.GetDirectoryName(AppPaths.DataJsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(AppPaths.DataJsonPath, root.ToString(Formatting.Indented));
    }
}
