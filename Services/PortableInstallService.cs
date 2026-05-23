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

            var ext = Path.GetExtension(archivePath).ToLowerInvariant();
            if ((ext == ".exe" || ext == ".phar") && (targetName == "minio" || targetName == "composer"))
            {
                return await Task.Run(() => InstallSingleFilePortable(archivePath, extractDir, targetName));
            }

            if (ext != ".zip")
                return (false, "当前仅支持 zip 绿色包自动解压，请手动解压到应用目录", null);

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
                _ => softwareName.ToLowerInvariant().Replace(' ', '-')
            };
        }

        private static void NormalizeExtractLayout(string extractDir, string targetName)
        {
            var subDirs = Directory.GetDirectories(extractDir);
            if (subDirs.Length != 1) return;

            var inner = subDirs[0];
            var innerName = Path.GetFileName(inner).ToLowerInvariant();

            if (innerName.Contains(targetName) || innerName.Contains("mysql") || innerName.Contains("redis") ||
                innerName.Contains("nginx") || innerName.Contains("node") || innerName.Contains("jdk") ||
                innerName.Contains("pgsql") || innerName.Contains("postgres"))
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
    }
}
