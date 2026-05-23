using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class MirrorConfigService
    {
        public IReadOnlyList<MirrorSource> GetMirrors(MirrorToolType tool) => tool switch
        {
            MirrorToolType.Pip => GetPipMirrors(),
            MirrorToolType.Npm => GetNpmMirrors(),
            MirrorToolType.Maven => GetMavenMirrors(),
            MirrorToolType.Composer => GetComposerMirrors(),
            MirrorToolType.Go => GetGoMirrors(),
            MirrorToolType.Pnpm => GetPnpmMirrors(),
            MirrorToolType.Yarn => GetYarnMirrors(),
            MirrorToolType.NuGet => GetNuGetMirrors(),
            _ => []
        };

        public IReadOnlyList<MirrorSource> GetPipMirrors() =>
        [
            new() { Key = "tsinghua", Name = "清华大学", Url = "https://pypi.tuna.tsinghua.edu.cn/simple", TrustedHost = "pypi.tuna.tsinghua.edu.cn" },
            new() { Key = "aliyun", Name = "阿里云", Url = "https://mirrors.aliyun.com/pypi/simple/", TrustedHost = "mirrors.aliyun.com" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/pypi/simple", TrustedHost = "mirrors.cloud.tencent.com" },
            new() { Key = "douban", Name = "豆瓣", Url = "https://pypi.douban.com/simple/", TrustedHost = "pypi.douban.com" },
            new() { Key = "ustc", Name = "中国科技大学", Url = "https://pypi.mirrors.ustc.edu.cn/simple/", TrustedHost = "pypi.mirrors.ustc.edu.cn" },
            new() { Key = "huawei", Name = "华为云", Url = "https://repo.huaweicloud.com/repository/pypi/simple", TrustedHost = "repo.huaweicloud.com" },
            new() { Key = "official", Name = "官方源", Url = "https://pypi.org/simple", TrustedHost = "pypi.org" }
        ];

        public IReadOnlyList<MirrorSource> GetNpmMirrors() =>
        [
            new() { Key = "npmmirror", Name = "npmmirror (淘宝)", Url = "https://registry.npmmirror.com" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/npm/" },
            new() { Key = "huawei", Name = "华为云", Url = "https://repo.huaweicloud.com/repository/npm/" },
            new() { Key = "ustc", Name = "中国科技大学", Url = "https://npmreg.proxy.ustclug.org/" },
            new() { Key = "official", Name = "官方源", Url = "https://registry.npmjs.org/" }
        ];

        public IReadOnlyList<MirrorSource> GetPnpmMirrors() =>
        [
            new() { Key = "npmmirror", Name = "npmmirror (淘宝)", Url = "https://registry.npmmirror.com" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/npm/" },
            new() { Key = "huawei", Name = "华为云", Url = "https://repo.huaweicloud.com/repository/npm/" },
            new() { Key = "official", Name = "官方源", Url = "https://registry.npmjs.org/" }
        ];

        public IReadOnlyList<MirrorSource> GetYarnMirrors() =>
        [
            new() { Key = "npmmirror", Name = "npmmirror (淘宝)", Url = "https://registry.npmmirror.com" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/npm/" },
            new() { Key = "official", Name = "官方源", Url = "https://registry.yarnpkg.com" }
        ];

        public IReadOnlyList<MirrorSource> GetMavenMirrors() =>
        [
            new() { Key = "aliyun", Name = "阿里云公共仓库", Url = "https://maven.aliyun.com/repository/public" },
            new() { Key = "huawei", Name = "华为云镜像", Url = "https://repo.huaweicloud.com/repository/maven/" },
            new() { Key = "tencent", Name = "腾讯云镜像", Url = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/" },
            new() { Key = "netease", Name = "网易镜像", Url = "https://mirrors.163.com/maven/repository/maven-public/" },
            new() { Key = "ustc", Name = "中科大镜像", Url = "https://mirrors.ustc.edu.cn/maven/" },
            new() { Key = "official", Name = "Maven中央仓库", Url = "https://repo1.maven.org/maven2/" }
        ];

        public IReadOnlyList<MirrorSource> GetComposerMirrors() =>
        [
            new() { Key = "aliyun", Name = "阿里云", Url = "https://mirrors.aliyun.com/composer/", TestUrl = "https://mirrors.aliyun.com/composer/packages.json" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/composer/", TestUrl = "https://mirrors.cloud.tencent.com/composer/packages.json" },
            new() { Key = "huawei", Name = "华为云", Url = "https://repo.huaweicloud.com/repository/php/", TestUrl = "https://repo.huaweicloud.com/repository/php/packages.json" },
            new() { Key = "cnpkg", Name = "中国全量镜像 (packagist.cn)", Url = "https://packagist.cn/", TestUrl = "https://packagist.cn/packages.json" },
            new() { Key = "sjtug", Name = "上海交通大学", Url = "https://mirrors.sjtug.sjtu.edu.cn/composer/", TestUrl = "https://mirrors.sjtug.sjtu.edu.cn/composer/packages.json" },
            new() { Key = "official", Name = "官方源", Url = "https://repo.packagist.org/", TestUrl = "https://repo.packagist.org/packages.json" }
        ];

        public IReadOnlyList<MirrorSource> GetGoMirrors() =>
        [
            new() { Key = "goproxy_cn", Name = "goproxy.cn", Url = "https://goproxy.cn,direct", TestUrl = "https://goproxy.cn" },
            new() { Key = "goproxy_io", Name = "goproxy.io", Url = "https://goproxy.io,direct", TestUrl = "https://goproxy.io" },
            new() { Key = "aliyun", Name = "阿里云", Url = "https://mirrors.aliyun.com/goproxy/,direct", TestUrl = "https://mirrors.aliyun.com/goproxy/" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.tencent.com/go/,direct", TestUrl = "https://mirrors.tencent.com/go/" },
            new() { Key = "ustc", Name = "中国科技大学", Url = "https://goproxy.ustc.edu.cn,direct", TestUrl = "https://goproxy.ustc.edu.cn" },
            new() { Key = "official", Name = "官方源", Url = "https://proxy.golang.org,direct", TestUrl = "https://proxy.golang.org" }
        ];

        public IReadOnlyList<MirrorSource> GetNuGetMirrors() =>
        [
            new() { Key = "azure_cn", Name = "Azure 中国", Url = "https://nuget.cdn.azure.cn/v3/index.json" },
            new() { Key = "huawei", Name = "华为云", Url = "https://repo.huaweicloud.com/repository/nuget/v3/index.json" },
            new() { Key = "tencent", Name = "腾讯云", Url = "https://mirrors.cloud.tencent.com/nuget/v3/index.json" },
            new() { Key = "official", Name = "官方源", Url = "https://api.nuget.org/v3/index.json" }
        ];

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

        public async Task<string?> GetCurrentMavenMirrorAsync()
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "settings.xml");
            if (!File.Exists(settingsPath)) return null;

            try
            {
                var doc = XDocument.Load(settingsPath);
                return doc.Descendants("mirror").Elements("url").FirstOrDefault()?.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCurrentNuGetMirrorAsync()
        {
            var nugetConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "NuGet.Config");
            if (!File.Exists(nugetConfig)) return null;

            try
            {
                var doc = XDocument.Load(nugetConfig);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var source = doc.Descendants(ns + "packageSource")
                    .FirstOrDefault(e => e.Attribute("key")?.Value is "nuget.org" or "Huawei" or "Tencent" or "AzureChina");
                return source?.Attribute("value")?.Value
                    ?? doc.Descendants(ns + "add").FirstOrDefault(e => e.Attribute("key")?.Value == "nuget.org")?.Attribute("value")?.Value;
            }
            catch
            {
                var (success, output) = await RunCommandAsync("dotnet", "nuget list source");
                if (success && output.Contains("https://"))
                {
                    var line = output.Split('\n').FirstOrDefault(l => l.Contains("https://") && l.Contains("[Enabled]"));
                    if (line != null)
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        return parts.LastOrDefault(p => p.StartsWith("https://"));
                    }
                }
                return null;
            }
        }

        public async Task<(bool Success, string Message)> ConfigurePipMirrorAsync(MirrorSource mirror)
        {
            var (indexResult, indexOutput) = await RunCommandAsync("pip", $"config set global.index-url {mirror.Url}");
            if (!indexResult)
                return (false, $"配置 pip 镜像失败: {indexOutput}");

            if (!string.IsNullOrEmpty(mirror.TrustedHost))
                await RunCommandAsync("pip", $"config set global.trusted-host {mirror.TrustedHost}");

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
            if (mirror.Key == "official")
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

            var mirrorId = mirror.Url.Split("//")[1].Split('/')[0].Replace('.', '-');
            var content = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <settings xmlns="http://maven.apache.org/SETTINGS/1.0.0"
                          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                          xsi:schemaLocation="http://maven.apache.org/SETTINGS/1.0.0
                          http://maven.apache.org/xsd/settings-1.0.0.xsd">
                  <mirrors>
                    <mirror>
                      <id>{mirrorId}</id>
                      <mirrorOf>*</mirrorOf>
                      <name>{mirror.Name}</name>
                      <url>{mirror.Url}</url>
                    </mirror>
                  </mirrors>
                </settings>
                """;

            await File.WriteAllTextAsync(settingsPath, content);
            return (true, $"Maven 镜像已配置为 {mirror.Name}");
        }

        public async Task<(bool Success, string Message)> ConfigureNuGetMirrorAsync(MirrorSource mirror)
        {
            var sourceName = mirror.Key == "official" ? "nuget.org" : $"Lava-{mirror.Key}";
            await RunCommandAsync("dotnet", $"nuget remove source {sourceName}");

            if (mirror.Key == "official")
            {
                var (addSuccess, addOutput) = await RunCommandAsync("dotnet",
                    $"nuget add source {mirror.Url} --name nuget.org");
                return addSuccess
                    ? (true, "NuGet 已恢复官方源")
                    : (false, $"配置 NuGet 官方源失败: {addOutput}");
            }

            var (success, output) = await RunCommandAsync("dotnet",
                $"nuget add source {mirror.Url} --name {sourceName}");
            if (!success)
                return (false, $"配置 NuGet 镜像失败: {output}");

            await RunCommandAsync("dotnet", $"nuget disable source nuget.org");
            return (true, $"NuGet 镜像已配置为 {mirror.Name}");
        }

        public async Task<List<MirrorSource>> TestMirrorSpeedAsync(IReadOnlyList<MirrorSource> mirrors, int timeoutSeconds = 5)
        {
            var results = new List<MirrorSource>();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            foreach (var mirror in mirrors)
            {
                var copy = new MirrorSource
                {
                    Key = mirror.Key,
                    Name = mirror.Name,
                    Url = mirror.Url,
                    TrustedHost = mirror.TrustedHost,
                    TestUrl = mirror.TestUrl
                };

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

                results.Add(copy);
            }

            return results.OrderBy(m => m.ResponseTimeMs ?? double.MaxValue).ToList();
        }

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
