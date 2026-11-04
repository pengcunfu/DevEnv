using System.IO;

namespace DevEnv.Host.SigXor.Toolkit.Models;

public static class ModelMigration
{
    public static Task CopyDirectoryAsync(
        string source,
        string destination,
        Action<string>? status,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            int index = 0;
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
                index++;
                status?.Invoke($"正在迁移模型 {index}/{files.Length}：{Path.GetFileName(file)}");
            }
        }, cancellationToken);
    }
}
