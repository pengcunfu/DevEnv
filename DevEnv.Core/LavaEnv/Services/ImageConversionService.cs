using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

namespace DevEnv.Core.LavaEnv.Services
{
    public class ImageConversionService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".ico",
        };

        public async Task<List<(string InputFile, string? OutputFile, bool Success, string? ErrorMessage)>> ConvertImagesAsync(
            List<string> inputFiles,
            string outputFormat,
            string? outputDirectory,
            int quality = 95,
            bool overwriteExisting = false,
            IProgress<ImageConversionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<(string InputFile, string? OutputFile, bool Success, string? ErrorMessage)>();

            for (int i = 0; i < inputFiles.Count; i++)
            {
                var inputFile = inputFiles[i];

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var inputExtension = Path.GetExtension(inputFile);
                    if (!SupportedExtensions.Contains(inputExtension))
                    {
                        results.Add((inputFile, null, false, $"不支持的文件格式: {inputExtension}"));
                        continue;
                    }

                    var outputExtension = GetFileExtension(outputFormat);
                    var fileName = Path.GetFileNameWithoutExtension(inputFile);
                    var outputFileName = $"{fileName}{outputExtension}";
                    var targetDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                        ? Path.GetDirectoryName(inputFile) ?? Directory.GetCurrentDirectory()
                        : outputDirectory;
                    var outputFile = Path.Combine(targetDirectory, outputFileName);

                    if (!overwriteExisting && File.Exists(outputFile))
                    {
                        results.Add((inputFile, outputFile, false, "输出文件已存在且未选择覆盖"));
                        continue;
                    }

                    await Task.Run(() => ConvertOne(inputFile, outputFile, outputFormat, quality), cancellationToken);
                    results.Add((inputFile, outputFile, true, null));
                }
                catch (Exception ex)
                {
                    results.Add((inputFile, null, false, ex.Message));
                }

                progress?.Report(new ImageConversionProgress
                {
                    ProcessedFiles = i + 1,
                    TotalFiles = inputFiles.Count,
                    CurrentFile = inputFile,
                });
            }

            return results;
        }

        private static void ConvertOne(string inputFile, string outputFile, string format, int quality)
        {
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var image = new MagickImage(inputFile);
            var magickFormat = ToMagickFormat(format);

            if (IsOpaqueFormat(format) && image.HasAlpha)
            {
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
            }

            image.Quality = (uint)Math.Clamp(quality, 1, 100);
            image.Format = magickFormat;
            image.Write(outputFile);
        }

        private static MagickFormat ToMagickFormat(string format)
        {
            var normalized = format?.Replace(".", "").ToUpperInvariant();
            return normalized switch
            {
                "JPG" or "JPEG" => MagickFormat.Jpeg,
                "PNG" => MagickFormat.Png,
                "BMP" => MagickFormat.Bmp,
                "GIF" => MagickFormat.Gif,
                "TIFF" or "TIF" => MagickFormat.Tiff,
                "WEBP" => MagickFormat.WebP,
                "ICO" => MagickFormat.Ico,
                _ => throw new NotSupportedException($"不支持的输出格式: {format}"),
            };
        }

        private static bool IsOpaqueFormat(string format)
        {
            var opaqueFormats = new[] { "JPG", "JPEG", "BMP" };
            return opaqueFormats.Contains(format?.Replace(".", "").ToUpperInvariant());
        }

        private static string GetFileExtension(string format)
        {
            var normalizedFormat = format?.Replace(".", "").ToUpperInvariant();
            return normalizedFormat switch
            {
                "JPG" or "JPEG" => ".jpg",
                "PNG" => ".png",
                "BMP" => ".bmp",
                "GIF" => ".gif",
                "TIFF" or "TIF" => ".tiff",
                "WEBP" => ".webp",
                "ICO" => ".ico",
                _ => throw new NotSupportedException($"不支持的格式: {format}"),
            };
        }
    }

    public class ImageConversionProgress
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
    }
}
