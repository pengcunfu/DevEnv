using System.Text;
using System.Text.Json;
using DevBox.Core.Models;
using DevEnv.Services;

namespace DevBox.Core.Services;

/// <summary>实用小工具：JSON 格式化、哈希计算、Hosts 编辑、图像格式转换。</summary>
public sealed class UtilityService
{
    public static string HostsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    public OperationResult FormatJson(string text, bool minify)
    {
        try
        {
            using var parsed = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var result = minify
                ? JsonSerializer.Serialize(parsed.RootElement)
                : JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
            return OperationResult.Success(result);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>计算文件或文本哈希；返回 (操作结果, 哈希详情)。</summary>
    public async Task<(OperationResult Result, HashResultView? Hash)> CalculateHashAsync(
        string? algorithm,
        string? path,
        string? text)
    {
        if (!Enum.TryParse<HashType>(algorithm, true, out var hashType))
            hashType = HashType.SHA256;

        var service = new HashCalculatorService();
        try
        {
            HashResult hash;
            if (!string.IsNullOrWhiteSpace(path))
            {
                hash = await service.CalculateFileHashAsync(path, hashType);
            }
            else if (!string.IsNullOrEmpty(text))
            {
                hash = await service.CalculateTextHashAsync(text, hashType);
            }
            else
            {
                return (OperationResult.Fail("请提供文件或文本"), null);
            }

            return (OperationResult.Success(), new HashResultView
            {
                Hash = hash.Hash,
                Algorithm = hash.Algorithm,
                FileSize = hash.FileSize > 0 ? hash.FileSize.ToString() : string.Empty,
                ElapsedMs = hash.CalculationTime.TotalMilliseconds,
            });
        }
        catch (Exception ex)
        {
            return (OperationResult.Fail(ex.Message), null);
        }
    }

    public OperationResult GetHosts(out HostsFileView? view)
    {
        view = null;
        try
        {
            var text = File.Exists(HostsPath) ? File.ReadAllText(HostsPath, Encoding.UTF8) : string.Empty;
            view = new HostsFileView
            {
                Path = HostsPath,
                Text = text,
                CanWrite = ElevationHelper.IsAdministrator(),
            };
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>读取 hosts 文件内容与可写状态（同步版本，便于 ViewModel 直接用）。</summary>
    public HostsFileView ReadHosts()
    {
        var result = GetHosts(out var view);
        return result.Ok && view is not null
            ? view
            : new HostsFileView { Path = HostsPath, Text = string.Empty, CanWrite = false };
    }

    public OperationResult SaveHosts(string text)
    {
        try
        {
            File.WriteAllText(HostsPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return OperationResult.Success("已保存");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult.Fail("需要管理员权限");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public OperationResult ElevateHosts() =>
        ElevationHelper.TryRunElevated(ElevationHelper.EditHostsArgument)
            ? OperationResult.Success("已请求管理员权限打开 Hosts 编辑")
            : OperationResult.Fail("用户取消了 UAC 或无法提权");

    public async Task<ImageConvertBatch> ConvertImagesAsync(
        IReadOnlyList<string> files,
        string format,
        string? outputDirectory,
        int quality)
    {
        var service = new ImageConversionService();
        var results = await service.ConvertImagesAsync(
            [.. files],
            string.IsNullOrWhiteSpace(format) ? "png" : format,
            outputDirectory,
            quality,
            overwriteExisting: true);

        var views = results.Select(r => new ImageConvertResult
        {
            Input = r.InputFile,
            Output = r.OutputFile,
            Success = r.Success,
            Error = r.ErrorMessage,
        }).ToList();

        return new ImageConvertBatch
        {
            Results = views,
            SuccessCount = views.Count(v => v.Success),
        };
    }
}