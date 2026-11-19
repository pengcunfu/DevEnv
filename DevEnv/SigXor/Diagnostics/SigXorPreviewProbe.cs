using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// 端到端预览探针：造真实文件，走完整链路加载内容，验证各类文件能渲染出内容视图模型。
/// 用法：DevEnv.exe --sigxor-preview-smoke [报告路径]
///
/// 与 <see cref="SigXorSmokeProbe"/> 的区别：那个只验证「窗口能加载」，
/// 这个验证「内容能加载」，能抓到 webassets 缺失、模型缺失之类的运行期问题。
///
/// 注意：加载链路里有 Dispatcher 调度，不能用 GetAwaiter().GetResult() 同步等待，
/// 必须整体异步，否则在 UI 线程上会死锁。
/// </summary>
public static class SigXorPreviewProbe
{
    public const string Argument = "--sigxor-preview-smoke";

    public static async Task<int> RunAsync(string[] args)
    {
        var report = new System.Text.StringBuilder();
        int failures = 0;

        async Task ProbeAsync(string name, Func<Task<object?>> action)
        {
            try
            {
                var content = await action();
                string detail = content is null ? "（null）" : content.GetType().Name;
                if (content is null)
                    failures++;
                report.AppendLine($"{(content is null ? "FAIL " : "OK    ")}{name} -> {detail}");
            }
            catch (Exception ex)
            {
                failures++;
                report.AppendLine($"FAIL  {name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        void Check(string name, bool ok, string? detail = null)
        {
            if (ok)
            {
                report.AppendLine($"OK    {name}");
                return;
            }

            failures++;
            report.AppendLine($"FAIL  {name}{(detail is null ? "" : ": " + detail)}");
        }

        static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
                await Task.Delay(20);
        }

        string dir = Path.Combine(Path.GetTempPath(), "sigxor-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string subDir = Path.Combine(dir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "sub.txt"), "sub folder\n", new UTF8Encoding(false));
        try
        {
            var settings = new Services.SettingsService();
            var backup = new Services.BackupService(settings);
            var preview = new ViewModels.PreviewViewModel(settings, backup);

            // 各类样本文件：覆盖 PreviewViewModel 的全部分支
            var samples = new (string Name, string FileName, string Content)[]
            {
                ("文本", "sample.txt", "hello devbox\nline2\n"),
                ("代码", "sample.cs", "namespace X { public class Y { } }\n"),
                ("Markdown", "sample.md", "# 标题\n\n正文 **加粗**\n"),
                ("网页", "sample.html", "<html><body><h1>hi</h1></body></html>"),
                ("CSV", "sample.csv", "name,age\nAlice,30\nBob,25\n"),
                ("SVG", "sample.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>"),
                ("JSON", "sample.json", "{ \"a\": 1 }"),
                ("YAML", "sample.yaml", "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: 测试配置\n"),
                ("无扩展名 YAML", "manifest", "---\napiVersion: v1\nkind: Pod\n"),
            };

            foreach (var (name, fileName, content) in samples)
            {
                string path = Path.Combine(dir, fileName);
                File.WriteAllText(path, content, new UTF8Encoding(false));

                await ProbeAsync($"预览 {name}", async () =>
                {
                    var entry = new DevEnv.Core.SigXor.FileEntry
                    {
                        Name = fileName,
                        FullPath = path,
                        Length = new FileInfo(path).Length,
                        Kind = DevEnv.Core.SigXor.FileTypeDetector.Detect(path),
                    };
                    await preview.LoadAsync(entry);
                    return preview.Content;
                });
            }

            // 二进制文件 → 十六进制视图
            string binPath = Path.Combine(dir, "sample.bin");
            File.WriteAllBytes(binPath, new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE });
            await ProbeAsync("预览 二进制", async () =>
            {
                var entry = new DevEnv.Core.SigXor.FileEntry
                {
                    Name = "sample.bin",
                    FullPath = binPath,
                    Length = new FileInfo(binPath).Length,
                    Kind = DevEnv.Core.SigXor.FileTypeDetector.Detect(binPath),
                };
                await preview.LoadAsync(entry);
                return preview.Content;
            });

            // 目录 → 文件管理器（目录导航 + 文件列表）
            await ProbeAsync("预览 目录（文件管理器）", async () =>
            {
                var entry = new DevEnv.Core.SigXor.FileEntry
                {
                    Name = "dir",
                    FullPath = dir,
                    IsDirectory = true,
                };
                await preview.LoadAsync(entry);
                return preview.Content;
            });

            // 文件夹预览完整流程：列表加载、打开文件、返回文件夹
            var shell = new ViewModels.ShellPreviewViewModel(settings, backup);
            shell.ResetFiles(
            [
                new DevEnv.Core.SigXor.FileEntry
                {
                    Name = "dir",
                    FullPath = dir,
                    IsDirectory = true,
                },
            ]);
            await WaitUntilAsync(() => shell.Preview.Content is ViewModels.FolderContentViewModel, 5_000);
            var folder = shell.Preview.Content as ViewModels.FolderContentViewModel;
            await WaitUntilAsync(() => folder is { Entries.Count: > 0 }, 5_000);
            Check("文件夹预览加载文件列表", folder is { Entries.Count: > 0 });

            if (folder is not null)
            {
                var sub = folder.Entries.FirstOrDefault(
                    entry => entry.IsDirectory && entry.Name == "sub");
                Check("文件夹预览包含子目录", sub is not null);
                if (sub is not null)
                {
                    folder.SelectedEntry = sub;
                    await folder.ActivateSelectedAsync();
                    await WaitUntilAsync(
                        () => string.Equals(folder.CurrentDirectory, subDir, StringComparison.OrdinalIgnoreCase)
                            && folder.Entries.Any(entry => entry.Name == "sub.txt"),
                        5_000);
                    Check("双击子目录可进入",
                        string.Equals(folder.CurrentDirectory, subDir, StringComparison.OrdinalIgnoreCase)
                        && folder.Entries.Any(entry => entry.Name == "sub.txt"));
                    Check("子目录可返回上级", folder.CanGoBack);

                    folder.NavigateUpCommand.Execute(null);
                    await WaitUntilAsync(
                        () => string.Equals(folder.CurrentDirectory, dir, StringComparison.OrdinalIgnoreCase)
                            && folder.Entries.Any(entry => entry.Name == "sample.txt"),
                        5_000);
                    Check("返回上级目录",
                        string.Equals(folder.CurrentDirectory, dir, StringComparison.OrdinalIgnoreCase)
                        && folder.Entries.Any(entry => entry.Name == "sample.txt"),
                        $"cwd={folder.CurrentDirectory}, status={folder.StatusText}, entries={folder.Entries.Count}");
                }

                var sample = folder.Entries.FirstOrDefault(
                    entry => !entry.IsDirectory && entry.Name == "sample.txt");
                Check("文件夹预览包含示例文件", sample is not null);
                if (sample is not null)
                {
                    folder.SelectedEntry = sample;
                    await folder.ActivateSelectedAsync();
                    await WaitUntilAsync(
                        () => shell.Preview.Content is ViewModels.TextContentViewModel, 5_000);
                    Check("从文件夹预览打开文件",
                        shell.Preview.Content is ViewModels.TextContentViewModel);
                    Check("预览历史可返回文件夹", shell.CanGoPrevious);

                    if (shell.CanGoPrevious)
                    {
                        shell.PreviousCommand.Execute(null);
                        await WaitUntilAsync(
                            () => shell.Preview.Content is ViewModels.FolderContentViewModel, 5_000);
                        Check("返回文件夹预览",
                            shell.Preview.Content is ViewModels.FolderContentViewModel);
                    }
                }
            }

            shell.DisposeContent();
        }
        catch (Exception ex)
        {
            failures++;
            report.AppendLine($"FATAL {ex}");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        string text = report.ToString();
        ProbeOutput.Line(text);
        ProbeOutput.Line($"失败 {failures} 项");

        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            try { File.WriteAllText(args[0], ProbeOutput.Flush()); } catch { }
        }

        return failures == 0 ? 0 : 1;
    }
}
