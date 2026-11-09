using System.Text.Json;
using System.Text.Json.Serialization;
using DevEnv.Core;
using DevEnv.Core.Translator.Models;

namespace DevEnv.Core.Translator.Services;

public sealed class ConfigService
{
    private const string LegacyFolderName = ".lava_translator";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;
    private AppConfig _config;

    /// <summary>配置目录：文档\FNSoftware\DevEnv\Translator。</summary>
    public ConfigService()
        : this(Path.Combine(AppPaths.UserDataDirectory, "Translator"), migrateLegacy: true)
    {
    }

    /// <summary>指定配置目录（测试用），不读取旧版用户目录。</summary>
    public ConfigService(string configDirectory)
        : this(configDirectory, migrateLegacy: false)
    {
    }

    private ConfigService(string configDirectory, bool migrateLegacy)
    {
        Directory.CreateDirectory(configDirectory);
        _configPath = Path.Combine(configDirectory, "config.json");
        if (migrateLegacy)
            MigrateLegacyConfig(_configPath);
        _config = Load();
    }

    public AppConfig Current => _config;

    public event EventHandler? ConfigChanged;

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            _config = CreateDefault();
            Save(_config);
            return _config;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            _config = MergeWithDefaults(loaded ?? CreateDefault());
            return _config;
        }
        catch
        {
            _config = CreateDefault();
            return _config;
        }
    }

    public bool Save(AppConfig? config = null)
    {
        try
        {
            _config = config ?? _config;
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(_configPath, json);
            ConfigChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void UpdateGeneral(GeneralConfig general)
    {
        _config.General = general;
        Save();
    }

    public string ConfigFilePath => _configPath;

    /// <summary>旧版把配置放在 %USERPROFILE%\.lava_translator，首次运行时迁入新目录。</summary>
    private static void MigrateLegacyConfig(string configPath)
    {
        if (File.Exists(configPath))
            return;

        try
        {
            var legacyDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                LegacyFolderName);
            var legacyPath = Path.Combine(legacyDir, "config.json");
            if (!File.Exists(legacyPath))
                return;

            File.Copy(legacyPath, configPath, overwrite: false);

            var remaining = Directory.GetFileSystemEntries(legacyDir);
            if (remaining.Length == 1 &&
                string.Equals(Path.GetFileName(remaining[0]), "config.json", StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(legacyDir, recursive: true);
            }
        }
        catch
        {
            // 迁移失败不影响本次启动，旧文件保留
        }
    }

    private static AppConfig CreateDefault() => new()
    {
        General = new GeneralConfig()
    };

    private static AppConfig MergeWithDefaults(AppConfig loaded)
    {
        var defaults = CreateDefault();
        loaded.General ??= defaults.General;
        return loaded;
    }
}
