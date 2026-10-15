using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using IImageEncoder = SixLabors.ImageSharp.Formats.IImageEncoder;
using Color = SixLabors.ImageSharp.Color;

namespace DevEnv.Services
{
    public class ImageConversionService
    {
        private readonly Dictionary<string, Func<Image, string, int, Task>> _converters;

        public ImageConversionService()
        {
            _converters = new Dictionary<string, Func<Image, string, int, Task>>(StringComparer.OrdinalIgnoreCase)
            {
                { ".jpg", ConvertToJpeg },
                { ".jpeg", ConvertToJpeg },
                { ".png", ConvertToPng },
                { ".bmp", ConvertToBmp },
                { ".gif", ConvertToGif },
                { ".tiff", ConvertToTiff },
                { ".tif", ConvertToTiff },
                { ".webp", ConvertToWebp },
                { ".ico", ConvertToIco }
            };
        }

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
            var supportedExtensions = _converters.Keys;

            for (int i = 0; i < inputFiles.Count; i++)
            {
                var inputFile = inputFiles[i];

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var inputExtension = Path.GetExtension(inputFile);
                    if (!supportedExtensions.Contains(inputExtension))
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

                    // 检查文件是否已存在
                    if (!overwriteExisting && File.Exists(outputFile))
                    {
                        results.Add((inputFile, outputFile, false, "输出文件已存在且未选择覆盖"));
                        continue;
                    }

                    // 转换图像
                    using (var image = await Image.LoadAsync(inputFile, cancellationToken))
                    {
                        // 处理透明背景（对于不支持透明度的格式）
                        if (IsOpaqueFormat(outputFormat) && HasTransparency(image))
                        {
                            var clonedImage = image.Clone(ctx => ConvertToOpaque(ctx));
                            await SaveImageAsync(clonedImage, outputFile, outputFormat, quality, cancellationToken);
                        }
                        else
                        {
                            await SaveImageAsync(image, outputFile, outputFormat, quality, cancellationToken);
                        }
                    }

                    results.Add((inputFile, outputFile, true, null));
                }
                catch (Exception ex)
                {
                    results.Add((inputFile, null, false, ex.Message));
                }

                // 报告进度
                progress?.Report(new ImageConversionProgress
                {
                    ProcessedFiles = i + 1,
                    TotalFiles = inputFiles.Count,
                    CurrentFile = inputFile
                });
            }

            return results;
        }

        private async Task SaveImageAsync(Image image, string outputFile, string format, int quality, CancellationToken cancellationToken)
        {
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encoder = GetEncoder(format, quality);
            await image.SaveAsync(outputFile, encoder, cancellationToken);
        }

        private IImageEncoder GetEncoder(string format, int quality)
        {
            var normalizedFormat = format?.Replace(".", "").ToUpperInvariant();

            return normalizedFormat switch
            {
                "JPG" or "JPEG" => new JpegEncoder { Quality = quality },
                "PNG" => new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression },
                "BMP" => new BmpEncoder(),
                "GIF" => new GifEncoder(),
                "TIFF" or "TIF" => new TiffEncoder(),
                "WEBP" => new WebpEncoder { Quality = quality },
                "ICO" => new BmpEncoder(),
                _ => throw new NotSupportedException($"不支持的输出格式: {format}")
            };
        }

        private static void ConvertToOpaque(IImageProcessingContext context)
        {
            context.BackgroundColor(Color.White);
        }

        private static bool HasTransparency(Image image)
        {
            return image.PixelType.BitsPerPixel > 24;
        }

        private bool IsOpaqueFormat(string format)
        {
            var opaqueFormats = new[] { "JPG", "JPEG", "BMP" };
            return opaqueFormats.Contains(format?.Replace(".", "").ToUpperInvariant());
        }

        private string GetFileExtension(string format)
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
                _ => throw new NotSupportedException($"不支持的格式: {format}")
            };
        }

        private async Task ConvertToJpeg(Image image, string outputPath, int quality)
        {
            var encoder = new JpegEncoder { Quality = quality };
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToPng(Image image, string outputPath, int quality)
        {
            var encoder = new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression };
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToBmp(Image image, string outputPath, int quality)
        {
            var encoder = new BmpEncoder();
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToGif(Image image, string outputPath, int quality)
        {
            var encoder = new GifEncoder();
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToTiff(Image image, string outputPath, int quality)
        {
            var encoder = new TiffEncoder();
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToWebp(Image image, string outputPath, int quality)
        {
            var encoder = new WebpEncoder { Quality = quality };
            await image.SaveAsync(outputPath, encoder);
        }

        private async Task ConvertToIco(Image image, string outputPath, int quality)
        {
            var encoder = new BmpEncoder();
            await image.SaveAsync(outputPath, encoder);
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
