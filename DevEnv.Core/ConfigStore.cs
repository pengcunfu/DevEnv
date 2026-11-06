using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevEnv.Core;

/// <summary>读写现有 data.json 中的 Config 段。</summary>
public sealed class ConfigStore
{
    private readonly object _gate = new();

    public JObject GetConfig()
    {
        lock (_gate)
        {
            var root = LoadRoot();
            return root["Config"] as JObject ?? CreateDefaultConfig();
        }
    }

    public JObject SaveConfig(JObject patch)
    {
        lock (_gate)
        {
            var root = LoadRoot();
            var config = root["Config"] as JObject ?? CreateDefaultConfig();
            foreach (var prop in patch.Properties())
                config[prop.Name] = prop.Value?.DeepClone();
            root["Config"] = config;
            SaveRoot(root);
            return config;
        }
    }

    private JObject LoadRoot()
    {
        if (!File.Exists(AppPaths.DataJsonPath))
        {
            var fresh = new JObject
            {
                ["Config"] = CreateDefaultConfig(),
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

    private static void SaveRoot(JObject root)
    {
        var dir = Path.GetDirectoryName(AppPaths.DataJsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(AppPaths.DataJsonPath, root.ToString(Formatting.Indented));
    }

    private static JObject CreateDefaultConfig() => new()
    {
        ["TrayIcon"] = true,
        ["SelfStarting"] = false,
        ["BaseEnvPath"] = @"D:\ProgramBox\env",
        ["Language"] = "zh-CN",
        ["DarkMode"] = false,
        ["ShowWindowHotkey"] = "Ctrl+Alt+D",
        ["QuickAppHotkey"] = "Alt+Space",
        ["WinKeyLauncher"] = true,
    };
}
