using System.Diagnostics;
using System.IO;
using DevEnv.Models;

namespace DevEnv.Services
{
    public static class SevenZipHelper
    {
        private const string Bundled7zrRelative = "tools/7zr.exe";

        public static bool Is7zArchive(string path)
        {
            var name = Path.GetFileName(path).ToLowerInvariant();
            return name.EndsWith(".7z", StringComparison.Ordinal) && !name.EndsWith(".7z.exe", StringComparison.Ordinal);
        }

        public static bool Is7zSelfExtracting(string path)
        {
            return Path.GetFileName(path).EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase);
        }

        public static string? Resolve7zExecutable(AppConfigService? configService = null)
        {
            var appsDir = ResolveAppsDir(configService);

            foreach (var candidate in Build7zCandidates(appsDir))
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            var bundled = Path.Combine(ResourcePaths.ResourcesDirectory, Bundled7zrRelative);
            return File.Exists(bundled) ? bundled : null;
        }

        private static IEnumerable<string> Build7zCandidates(string appsDir)
        {
            foreach (var folder in new[] { "7zip", "7-zip" })
            {
                var baseDir = Path.Combine(appsDir, folder);
                if (Environment.Is64BitOperatingSystem)
                    yield return Path.Combine(baseDir, "x64", "7za.exe");

                yield return Path.Combine(baseDir, "7z.exe");
                yield return Path.Combine(baseDir, "7za.exe");
                yield return Path.Combine(baseDir, "7zr.exe");
            }
        }

        public static async Task<(bool Success, string Message)> Extract7zAsync(
            string archivePath, string extractDir, AppConfigService? configService = null)
        {
            var sevenZip = Resolve7zExecutable(configService);
            if (sevenZip == null)
                return (false, "未找到 7-Zip/7zr，请先安装 7-Zip 或确保内置 7zr.exe 存在");

            Directory.CreateDirectory(extractDir);
            return await Run7zAsync(sevenZip, $"x \"{archivePath}\" -o\"{extractDir}\" -y");
        }

        public static async Task<(bool Success, string Message)> Extract7zSelfExtractingAsync(
            string sfxPath, string extractDir, AppConfigService? configService = null)
        {
            Directory.CreateDirectory(extractDir);

            var direct = await RunProcessAsync(sfxPath, $"-o\"{extractDir}\" -y", Path.GetDirectoryName(sfxPath));
            if (direct.Success)
                return direct;

            var sevenZip = Resolve7zExecutable(configService);
            if (sevenZip == null)
                return (false, $"自解压失败: {direct.Message}");

            return await Run7zAsync(sevenZip, $"x \"{sfxPath}\" -o\"{extractDir}\" -y");
        }

        private static async Task<(bool Success, string Message)> Run7zAsync(
            string sevenZip, string arguments)
        {
            return await RunProcessAsync(sevenZip, arguments, Path.GetDirectoryName(sevenZip));
        }

        private static async Task<(bool Success, string Message)> RunProcessAsync(
            string fileName, string arguments, string? workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return (false, "无法启动解压进程");

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                    return (true, "解压完成");

                var detail = string.IsNullOrWhiteSpace(stderr) ? $"退出码 {process.ExitCode}" : stderr.Trim();
                return (false, detail);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string ResolveAppsDir(AppConfigService? configService)
        {
            if (configService != null)
            {
                var settings = configService.Load();
                if (!string.IsNullOrWhiteSpace(settings.AppsDir))
                    return settings.AppsDir;
            }

            return AppPaths.AppsDir;
        }
    }
}
