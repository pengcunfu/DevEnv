using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Core.LavaEnv.Services
{
    /// <summary>
    /// 镜像源读取与配置。
    /// 镜像清单不再写死在代码里：优先读取文档\FNSoftware\DevEnv\mirrors_config.json，
    /// 首次运行时从随程序发布的 Resources\mirrors_config.json 复制；
    /// 用户文件缺失或解析失败时回退到内置模板。
    /// </summary>
    public class MirrorConfigService
    {
        private const string ManagedMavenMirrorId = "lava";
        private const string ManagedNuGetSourcePrefix = "Lava-";

        private readonly string _catalogPath;
        private readonly string? _templatePath;

        public MirrorConfigService()
            : this(LavaPaths.MirrorsConfigFile, ResourcePaths.GetBundledResourcePath(ResourcePaths.MirrorsConfigFile))
        {
        }

        /// <summary>指定清单路径，便于测试；templatePath 为内置模板。</summary>
        public MirrorConfigService(string catalogPath, string? templatePath = null)
        {
            _catalogPath = catalogPath;
            _templatePath = templatePath;
            EnsureCatalogFile();
        }

        /// <summary>当前生效的镜像清单文件路径。</summary>
        public string CatalogPath => _catalogPath;

        public IReadOnlyList<MirrorSource> GetMirrors(MirrorToolType tool)
        {
            var catalog = LoadCatalog();
            return catalog.TryGetValue(tool.ToString(), out var mirrors) ? mirrors : [];
        }

        public IReadOnlyDictionary<string, List<MirrorSource>> LoadCatalog()
        {
            // 运行期间删掉文件视为恢复默认：重新复制模板后再读。
            EnsureCatalogFile();
            return TryLoadCatalog(_catalogPath)
                ?? TryLoadCatalog(_templatePath)
                ?? MirrorCatalog.Empty;
        }

        /// <summary>首次运行时把内置模板复制到用户数据目录，供用户直接编辑。</summary>
        private void EnsureCatalogFile()
        {
            try
            {
                if (File.Exists(_catalogPath) || string.IsNullOrWhiteSpace(_templatePath) || !File.Exists(_templatePath))
                    return;

                var directory = Path.GetDirectoryName(_catalogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(_templatePath, _catalogPath);
            }
            catch
            {
                // 复制失败不影响读取：LoadCatalog 会回退到内置模板
            }
        }

        private static Dictionary<string, List<MirrorSource>>? TryLoadCatalog(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                return MirrorCatalog.Parse(File.ReadAllText(path));
            }
            catch
            {
                // 用户手改的文件可能被写坏；返回 null 让调用方回退到内置模板。
                return null;
            }
        }

        public async Task<string?> GetCurrentMirrorAsync(MirrorToolType tool) => tool switch
        {
            MirrorToolType.Pip => await GetCurrentPipMirrorAsync(),
            MirrorToolType.Npm => await GetCurrentNpmMirrorAsync(),
            MirrorToolType.Maven => await GetCurrentMavenMirrorAsync(),
            MirrorToolType.Composer => await GetCurrentComposerMirrorAsync(),
            MirrorToolType.Go => await GetCurrentGoMirrorAsync(),
            MirrorToolType.Pnpm => await GetCurrentPnpmMirrorAsync(),
            MirrorToolType.Yarn => await GetCurrentYarnMirrorAsync(),
            MirrorToolType.NuGet => await GetCurrentNuGetMirrorAsync(),
            _ => null
        };

        public Task<(bool Success, string Message)> ConfigureMirrorAsync(MirrorToolType tool, MirrorSource mirror) => tool switch
        {
            MirrorToolType.Pip => ConfigurePipMirrorAsync(mirror),
            MirrorToolType.Npm => ConfigureNpmMirrorAsync(mirror),
            MirrorToolType.Maven => ConfigureMavenMirrorAsync(mirror),
            MirrorToolType.Composer => ConfigureComposerMirrorAsync(mirror),
            MirrorToolType.Go => ConfigureGoMirrorAsync(mirror),
            MirrorToolType.Pnpm => ConfigurePnpmMirrorAsync(mirror),
            MirrorToolType.Yarn => ConfigureYarnMirrorAsync(mirror),
            MirrorToolType.NuGet => ConfigureNuGetMirrorAsync(mirror),
            _ => Task.FromResult((false, "未知工具类型"))
        };

        public static string GetToolDisplayName(MirrorToolType tool) => tool switch
        {
            MirrorToolType.Pip => "pip",
            MirrorToolType.Npm => "npm",
            MirrorToolType.Maven => "Maven",
            MirrorToolType.Composer => "Composer",
            MirrorToolType.Go => "Go",
            MirrorToolType.Pnpm => "pnpm",
            MirrorToolType.Yarn => "Yarn",
            MirrorToolType.NuGet => "NuGet",
            _ => tool.ToString()
        };

        public async Task<string?> GetCurrentPipMirrorAsync()
        {
            var (success, output) = await RunCommandAsync("pip", "config get global.index-url");
            return success ? output.Trim() : null;
        }

        public async Task<string?> GetCurrentNpmMirrorAsync()
        {
            var (success, output) = await RunCommandAsync("npm", "config get registry");
            return success ? output.Trim().Trim('"') : null;
        }

        public async Task<string?> GetCurrentPnpmMirrorAsync()
        {
            var (success, output) = await RunCommandAsync("pnpm", "config get registry");
            return success ? output.Trim().Trim('"') : null;
        }

        public async Task<string?> GetCurrentYarnMirrorAsync()
        {
            var (success, output) = await RunCommandAsync("yarn", "config get registry");
            return success ? output.Trim().Trim('"') : null;
        }

        public async Task<string?> GetCurrentGoMirrorAsync()
        {
            var (success, output) = await RunCommandAsync("go", "env GOPROXY");
            return success ? output.Trim() : null;
        }

        public async Task<string?> GetCurrentComposerMirrorAsync()
        {
            var composerHome = GetComposerHome();
            var configFile = Path.Combine(composerHome, "config.json");
            if (!File.Exists(configFile)) return null;

            try
            {
                var json = await File.ReadAllTextAsync(configFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("repositories", out var repos) &&
                    repos.TryGetProperty("packagist.org", out var packagist) &&
                    packagist.TryGetProperty("url", out var url))
                {
                    return url.GetString();
                }
            }
            catch { }

            var (success, output) = await RunCommandAsync("composer", "config --global repos.packagist.org.url");
            return success && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
        }

        public Task<string?> GetCurrentMavenMirrorAsync() => Task.FromResult(ReadMavenMirrorUrl());

        /// <summary>读取 settings.xml 中生效的镜像地址（优先 Lava 托管的条目）。</summary>
        private static string? ReadMavenMirrorUrl()
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "settings.xml");
            if (!File.Exists(settingsPath)) return null;

            try
            {
                var doc = XDocument.Load(settingsPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var mirrors = doc.Descendants(ns + "mirror").ToList();
                var managed = mirrors.FirstOrDefault(m => (string?)m.Element(ns + "id") == ManagedMavenMirrorId);
                return (managed ?? mirrors.FirstOrDefault())?.Element(ns + "url")?.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCurrentNuGetMirrorAsync()
        {
            // --format short 输出「状态 地址」，不受界面语言影响；状态 E 表示已启用。
            var (listed, listedOutput) = await RunCommandAsync("dotnet", "nuget list source --format short");
            if (listed)
            {
                foreach (var line in listedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && parts[0].Equals("E", StringComparison.OrdinalIgnoreCase)
                        && parts[1].StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        return parts[1];
                }
            }

            var nugetConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "NuGet.Config");
            if (!File.Exists(nugetConfig)) return null;

            try
            {
                var doc = XDocument.Load(nugetConfig);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var sources = doc.Descendants(ns + "packageSources")
                    .Elements(ns + "add")
                    .Where(e => e.Attribute("key") is not null)
                    .ToList();
                var source = sources.FirstOrDefault(e => e.Attribute("key")!.Value.StartsWith(ManagedNuGetSourcePrefix, StringComparison.OrdinalIgnoreCase))
                    ?? sources.FirstOrDefault(e => string.Equals(e.Attribute("key")!.Value, "nuget.org", StringComparison.OrdinalIgnoreCase));
                return source?.Attribute("value")?.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool Success, string Message)> ConfigurePipMirrorAsync(MirrorSource mirror)
        {
            var (indexResult, indexOutput) = await RunCommandAsync("pip", $"config set global.index-url {mirror.Url}");
            if (!indexResult)
                return (false, $"配置 pip 镜像失败: {indexOutput}");

            // 切回官方源时必须清掉上一家镜像留下的 trusted-host，否则 pip 会继续信任旧主机。
            if (!string.IsNullOrWhiteSpace(mirror.TrustedHost))
                await RunCommandAsync("pip", $"config set global.trusted-host {mirror.TrustedHost}");
            else
                await RunCommandAsync("pip", "config unset global.trusted-host");

            return (true, $"pip 镜像已配置为 {mirror.Name}");
        }

        public async Task<(bool Success, string Message)> ConfigureNpmMirrorAsync(MirrorSource mirror)
        {
            var (success, output) = await RunCommandAsync("npm", $"config set registry {mirror.Url}");
            return success
                ? (true, $"npm 镜像已配置为 {mirror.Name}")
                : (false, $"配置 npm 镜像失败: {output}");
        }

        public async Task<(bool Success, string Message)> ConfigurePnpmMirrorAsync(MirrorSource mirror)
        {
            var (success, output) = await RunCommandAsync("pnpm", $"config set registry {mirror.Url}");
            return success
                ? (true, $"pnpm 镜像已配置为 {mirror.Name}")
                : (false, $"配置 pnpm 镜像失败: {output}");
        }

        public async Task<(bool Success, string Message)> ConfigureYarnMirrorAsync(MirrorSource mirror)
        {
            var (success, output) = await RunCommandAsync("yarn", $"config set registry {mirror.Url}");
            return success
                ? (true, $"Yarn 镜像已配置为 {mirror.Name}")
                : (false, $"配置 Yarn 镜像失败: {output}");
        }

        public async Task<(bool Success, string Message)> ConfigureGoMirrorAsync(MirrorSource mirror)
        {
            var (success, output) = await RunCommandAsync("go", $"env -w GOPROXY={mirror.Url}");
            return success
                ? (true, $"Go 代理已配置为 {mirror.Name}")
                : (false, $"配置 Go 代理失败: {output}");
        }

        public async Task<(bool Success, string Message)> ConfigureComposerMirrorAsync(MirrorSource mirror)
        {
            if (mirror.IsOfficial)
            {
                var (unsetSuccess, unsetOutput) = await RunCommandAsync("composer", "config --global --unset repos.packagist.org");
                return unsetSuccess
                    ? (true, "Composer 已恢复官方源")
                    : (false, $"恢复 Composer 官方源失败: {unsetOutput}");
            }

            var url = mirror.Url.TrimEnd('/') + "/";
            var (success, output) = await RunCommandAsync("composer",
                $"config --global repos.packagist.org composer {url}");
            return success
                ? (true, $"Composer 镜像已配置为 {mirror.Name}")
                : (false, $"配置 Composer 镜像失败: {output}");
        }

        public async Task<(bool Success, string Message)> ConfigureMavenMirrorAsync(MirrorSource mirror)
        {
            var m2Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2");
            Directory.CreateDirectory(m2Dir);
            var settingsPath = Path.Combine(m2Dir, "settings.xml");

            if (File.Exists(settingsPath))
            {
                var backupPath = settingsPath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(settingsPath, backupPath, true);
            }

            try
            {
                var existing = File.Exists(settingsPath) ? await File.ReadAllTextAsync(settingsPath) : null;
                var managedUrls = GetMirrors(MirrorToolType.Maven).Select(m => m.Url);
                var content = ApplyMavenMirror(existing, managedUrls, mirror);
                await File.WriteAllTextAsync(settingsPath, content);
            }
            catch (Exception ex)
            {
                return (false, $"写入 Maven settings.xml 失败: {ex.Message}");
            }

            return (true, $"Maven 镜像已配置为 {mirror.Name}");
        }

        /// <summary>
        /// 把 Lava 托管的 mirror 写入 settings.xml 文本，保留用户已有的 servers / profiles 等配置。
        /// 纯函数：settingsXml 为 null 时生成新文件，便于测试与排查。
        /// </summary>
        public static string ApplyMavenMirror(string? settingsXml, IEnumerable<string> managedUrls, MirrorSource mirror)
        {
            XDocument doc;
            XNamespace ns;
            XElement mirrors;

            if (string.IsNullOrWhiteSpace(settingsXml))
            {
                ns = XNamespace.Get("http://maven.apache.org/SETTINGS/1.0.0");
                mirrors = new XElement(ns + "mirrors");
                doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(ns + "settings",
                        new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                        new XAttribute(XName.Get("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"),
                            "http://maven.apache.org/SETTINGS/1.0.0 http://maven.apache.org/xsd/settings-1.0.0.xsd"),
                        mirrors));
            }
            else
            {
                doc = XDocument.Parse(settingsXml);
                ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var root = doc.Root ?? throw new InvalidOperationException("settings.xml 缺少根节点");
                mirrors = root.Element(ns + "mirrors") ?? new XElement(ns + "mirrors");
                if (mirrors.Parent is null)
                    root.Add(mirrors);
            }

            // 旧版本用「域名-替换点」当 id，升级后同样的地址会以新 id 再加一条；
            // 这里连同清单内出现过的地址一起清掉，避免旧 mirror 继续排在前面生效。
            var urls = managedUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.TrimEnd('/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            mirrors.Elements(ns + "mirror")
                .Where(m => IsManagedMavenMirror(m, ns, urls))
                .Remove();
            mirrors.Add(new XElement(ns + "mirror",
                new XElement(ns + "id", ManagedMavenMirrorId),
                new XElement(ns + "mirrorOf", "*"),
                new XElement(ns + "name", mirror.Name),
                new XElement(ns + "url", mirror.Url)));

            using var writer = new StringWriter();
            doc.Save(writer);
            return writer.ToString();
        }

        private static bool IsManagedMavenMirror(XElement mirror, XNamespace ns, HashSet<string> managedUrls)
        {
            if ((string?)mirror.Element(ns + "id") == ManagedMavenMirrorId)
                return true;

            var url = (string?)mirror.Element(ns + "url");
            return !string.IsNullOrWhiteSpace(url) && managedUrls.Contains(url.TrimEnd('/'));
        }

        public async Task<(bool Success, string Message)> ConfigureNuGetMirrorAsync(MirrorSource mirror)
        {
            // 切换镜像前清掉上一次由本工具添加的源，避免旧源继续参与还原。
            foreach (var managedSource in GetManagedNuGetSourceNames())
                await RunCommandAsync("dotnet", $"nuget remove source \"{managedSource}\"");

            if (mirror.IsOfficial)
            {
                var (updated, _) = await RunCommandAsync("dotnet", $"nuget update source nuget.org --source {mirror.Url}");
                if (!updated)
                    await RunCommandAsync("dotnet", $"nuget add source {mirror.Url} --name nuget.org");

                var (enabled, enableOutput) = await RunCommandAsync("dotnet", "nuget enable source nuget.org");
                return enabled
                    ? (true, "NuGet 已恢复官方源")
                    : (false, $"配置 NuGet 官方源失败: {enableOutput}");
            }

            var sourceName = GetNuGetSourceName(mirror);
            var (sourceUpdated, _) = await RunCommandAsync("dotnet", $"nuget update source \"{sourceName}\" --source {mirror.Url}");
            if (!sourceUpdated)
            {
                var (added, addOutput) = await RunCommandAsync("dotnet", $"nuget add source {mirror.Url} --name \"{sourceName}\"");
                if (!added)
                    return (false, $"配置 NuGet 镜像失败: {addOutput}");
            }

            await RunCommandAsync("dotnet", $"nuget enable source \"{sourceName}\"");
            await RunCommandAsync("dotnet", "nuget disable source nuget.org");
            return (true, $"NuGet 镜像已配置为 {mirror.Name}");
        }

        private static string GetNuGetSourceName(MirrorSource mirror)
        {
            var cleaned = new string(mirror.Key
                .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')
                .ToArray())
                .Trim('-');
            return ManagedNuGetSourcePrefix + (string.IsNullOrEmpty(cleaned) ? "custom" : cleaned);
        }

        /// <summary>NuGet 用户级配置里由本工具添加的源名称。</summary>
        private static List<string> GetManagedNuGetSourceNames()
        {
            var names = new List<string>();
            var nugetConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "NuGet.Config");
            if (!File.Exists(nugetConfig)) return names;

            try
            {
                var doc = XDocument.Load(nugetConfig);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                foreach (var add in doc.Descendants(ns + "packageSources").Elements(ns + "add"))
                {
                    var key = add.Attribute("key")?.Value;
                    if (!string.IsNullOrWhiteSpace(key) &&
                        key.StartsWith(ManagedNuGetSourcePrefix, StringComparison.OrdinalIgnoreCase) &&
                        !names.Contains(key, StringComparer.OrdinalIgnoreCase))
                        names.Add(key);
                }
            }
            catch
            {
                // 配置不可读时跳过清理，后面的 add/update 仍会给出结果
            }

            return names;
        }

        public async Task<List<MirrorSource>> TestMirrorSpeedAsync(IReadOnlyList<MirrorSource> mirrors, int timeoutSeconds = 5)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var tasks = mirrors.Select(mirror => Clone(mirror)).Select(copy => MeasureAsync(client, copy));
            var results = await Task.WhenAll(tasks);
            return results.OrderBy(m => m.ResponseTimeMs ?? double.MaxValue).ToList();
        }

        private static async Task<MirrorSource> MeasureAsync(HttpClient client, MirrorSource copy)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var response = await client.GetAsync(copy.SpeedTestUrl, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();
                copy.ResponseTimeMs = response.IsSuccessStatusCode ? sw.Elapsed.TotalMilliseconds : null;
            }
            catch
            {
                copy.ResponseTimeMs = null;
            }

            return copy;
        }

        private static MirrorSource Clone(MirrorSource mirror) => new()
        {
            Key = mirror.Key,
            Name = mirror.Name,
            Url = mirror.Url,
            TrustedHost = mirror.TrustedHost,
            TestUrl = mirror.TestUrl,
            Official = mirror.Official
        };

        public async Task<bool> IsToolInstalledAsync(string tool, string versionArg = "--version")
        {
            var (success, _) = await RunCommandAsync(tool, versionArg);
            return success;
        }

        private static string GetComposerHome()
        {
            var env = Environment.GetEnvironmentVariable("COMPOSER_HOME");
            if (!string.IsNullOrEmpty(env)) return env;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Composer");
        }

        private static async Task<(bool Success, string Output)> RunCommandAsync(string fileName, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var combined = string.IsNullOrWhiteSpace(output) ? error : output;
                return (process.ExitCode == 0, combined.Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
