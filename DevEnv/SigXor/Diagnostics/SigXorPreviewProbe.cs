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

        string dir = Path.Combine(Path.GetTempPath(), "sigxor-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
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

            // 目录 → 信息视图
            await ProbeAsync("预览 目录", async () =>
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
