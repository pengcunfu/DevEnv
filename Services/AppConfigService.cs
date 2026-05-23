using System.IO;
using System.Text.Json;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class AppConfigService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private readonly string _configPath;

        public AppConfigService()
        {
            AppPaths.EnsureDirectories();
            _configPath = AppPaths.ConfigFile;
            EnsureRuntimeConfig();
        }

        /// <summary>
        /// 运行时配置位于 D:\devenv\config.json；
        /// 若不存在则从内置模板 Resources/config.json 复制。
        /// </summary>
        private void EnsureRuntimeConfig()
        {
            if (File.Exists(_configPath))
                return;

            var templatePath = ResourcePaths.GetBundledResourcePath(ResourcePaths.DefaultConfigFile);
            if (File.Exists(templatePath))
            {
                File.Copy(templatePath, _configPath);
                return;
            }

            Save(CreateDefaultSettings());
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                        return MergeDefaults(settings);
                }
            }
            catch
            {
                // fall through to defaults
            }

            var defaults = CreateDefaultSettings();
            Save(defaults);
            return defaults;
        }

        public void Save(AppSettings settings)
        {
            AppPaths.EnsureDirectories();
            var json = JsonSerializer.Serialize(MergeDefaults(settings), JsonOptions);
            File.WriteAllText(_configPath, json);
        }

        public static AppSettings CreateDefaultSettings() => new()
        {
            CacheDir = AppPaths.CacheDir,
            AppsDir = AppPaths.AppsDir
        };

        private static AppSettings MergeDefaults(AppSettings settings)
        {
            var defaults = CreateDefaultSettings();
            if (string.IsNullOrWhiteSpace(settings.CacheDir))
                settings.CacheDir = defaults.CacheDir;
            if (string.IsNullOrWhiteSpace(settings.AppsDir))
                settings.AppsDir = defaults.AppsDir;
            if (settings.MaxWorkers <= 0)
                settings.MaxWorkers = defaults.MaxWorkers;
            if (settings.ChunkSizeMb <= 0)
                settings.ChunkSizeMb = defaults.ChunkSizeMb;
            if (settings.Timeout <= 0)
                settings.Timeout = defaults.Timeout;
            if (settings.MaxRetries <= 0)
                settings.MaxRetries = defaults.MaxRetries;
            return settings;
        }
    }
}
