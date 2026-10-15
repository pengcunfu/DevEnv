using System.IO;
using System.IO.Compression;
using DevEnv.Models;

namespace DevEnv.Services
{
    public class PortableInstallService
    {
        private readonly AppConfigService _configService;

        public PortableInstallService(AppConfigService configService)
        {
            _configService = configService;
        }

        public async Task<(bool Success, string Message, string? ExtractPath)> ExtractPortableAsync(
            string archivePath, string softwareName, string version)
        {
            if (!File.Exists(archivePath))
                return (false, "文件不存在", null);

            var settings = _configService.Load();
            var appsDir = string.IsNullOrWhiteSpace(settings.AppsDir) ? AppPaths.AppsDir : settings.AppsDir;

            var targetName = GetTargetFolderName(softwareName);
            var extractDir = Path.Combine(appsDir, targetName);

            if (SevenZipHelper.Is7zSelfExtracting(archivePath))
                return await Extract7zSfxPortableAsync(archivePath, extractDir, targetName);

            if (SevenZipHelper.Is7zArchive(archivePath))
                return await Extract7zPortableAsync(archivePath, extractDir, targetName);

            if (IsTarGzArchive(archivePath))
                return await Extract7zPortableAsync(archivePath, extractDir, targetName);

            var ext = Path.GetExtension(archivePath).ToLowerInvariant();
            if ((ext == ".exe" || ext == ".phar") && (targetName == "minio" || targetName == "composer"))
            {
                return await Task.Run(() => InstallSingleFilePortable(archivePath, extractDir, targetName));
            }

            if (ext != ".zip")
                return (false, "当前仅支持 zip/7z 绿色包自动解压，请手动解压到应用目录", null);

            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                Directory.CreateDirectory(extractDir);
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractDir, true));
                NormalizeExtractLayout(extractDir, targetName);

                return (true, $"已解压到 {extractDir}", extractDir);
            }
            catch (Exception ex)
            {
                return (false, $"解压失败: {ex.Message}", null);
            }
        }

        private async Task<(bool Success, string Message, string? ExtractPath)> Extract7zPortableAsync(
            string archivePath, string extractDir, string targetName)
        {
            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                var (success, message) = await SevenZipHelper.Extract7zAsync(archivePath, extractDir, _configService);
                if (!success)
                    return (false, $"解压失败: {message}", null);

                if (targetName == "7zip")
                    Normalize7ZipLayout(extractDir);

                NormalizeExtractLayout(extractDir, targetName);
                return (true, $"已解压到 {extractDir}", extractDir);
            }
            catch (Exception ex)
            {
                return (false, $"解压失败: {ex.Message}", null);
            }
        }

        private async Task<(bool Success, string Message, string? ExtractPath)> Extract7zSfxPortableAsync(
            string sfxPath, string extractDir, string targetName)
        {
            try
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                var (success, message) = await SevenZipHelper.Extract7zSelfExtractingAsync(
                    sfxPath, extractDir, _configService);
                if (!success)
                    return (false, $"解压失败: {message}", null);

                NormalizeExtractLayout(extractDir, targetName);
                return (true, $"已解压到 {extractDir}", extractDir);
            }
            catch (Exception ex)
            {
                return (false, $"解压失败: {ex.Message}", null);
            }
        }

        private static (bool Success, string Message, string? ExtractPath) InstallSingleFilePortable(
            string filePath, string targetDir, string targetName)
        {
            try
            {
                Directory.CreateDirectory(targetDir);
                var destFile = Path.Combine(targetDir, Path.GetFileName(filePath));
                File.Copy(filePath, destFile, true);
                return (true, $"已安装到 {targetDir}", targetDir);
            }
            catch (Exception ex)
            {
                return (false, $"安装失败: {ex.Message}", null);
            }
        }

        private static string GetTargetFolderName(string softwareName)
        {
            return softwareName.ToLowerInvariant() switch
            {
                "mysql" => "mysql",
                "postgresql" => "postgresql",
                "redis" => "redis",
                "mongodb" => "mongodb",
                "minio" => "minio",
                "nginx" => "nginx",
                "openjdk" => "java",
                "python" => "python",
                "node.js" => "nodejs",
                "php" => "php",
                "apache maven" => "maven",
                "git" => "git",
                "go" => "go",
                "7-zip" => "7zip",
                "7zip" => "7zip",
                "kafka" => "kafka",
                "apache kafka" => "kafka",
                _ => softwareName.ToLowerInvariant().Replace(' ', '-')
            };
        }

        private static void Normalize7ZipLayout(string extractDir)
        {
            if (!Environment.Is64BitOperatingSystem) return;

            var x64Dir = Path.Combine(extractDir, "x64");
            if (!Directory.Exists(x64Dir)) return;

            foreach (var entry in Directory.GetFileSystemEntries(x64Dir))
            {
                var dest = Path.Combine(extractDir, Path.GetFileName(entry));
                if (Directory.Exists(entry))
                {
                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    Directory.Move(entry, dest);
                }
                else
                {
                    if (File.Exists(dest))
                        File.Delete(dest);
                    File.Move(entry, dest);
                }
            }

            Directory.Delete(x64Dir, true);
        }

        private static void NormalizeExtractLayout(string extractDir, string targetName)
        {
            var subDirs = Directory.GetDirectories(extractDir);
            if (subDirs.Length != 1) return;

            var inner = subDirs[0];
            var innerName = Path.GetFileName(inner).ToLowerInvariant();

            if (innerName.Contains(targetName) || innerName.Contains("mysql") || innerName.Contains("redis") ||
                innerName.Contains("nginx") || innerName.Contains("node") || innerName.Contains("jdk") ||
                innerName.Contains("pgsql") || innerName.Contains("postgres") || innerName.Contains("kafka") ||
                targetName == "go" && innerName.StartsWith("go"))
            {
                foreach (var entry in Directory.GetFileSystemEntries(inner))
                {
                    var dest = Path.Combine(extractDir, Path.GetFileName(entry));
                    if (Directory.Exists(entry))
                        Directory.Move(entry, dest);
                    else
                        File.Move(entry, dest);
                }
                Directory.Delete(inner, true);
            }
        }

        private static bool IsTarGzArchive(string path)
        {
            var name = path.ToLowerInvariant();
            return name.EndsWith(".tgz", StringComparison.Ordinal) ||
                   name.EndsWith(".tar.gz", StringComparison.Ordinal);
        }
    }
}
