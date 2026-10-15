using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DevEnv.Services
{
    public enum HashType
    {
        MD5,
        SHA1,
        SHA256,
        SHA384,
        SHA512
    }

    public class HashResult
    {
        public string Hash { get; set; } = string.Empty;
        public string Algorithm { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public TimeSpan CalculationTime { get; set; }
    }

    public class HashCalculatorService
    {
        private const int BufferSize = 8192; // 8KB buffer for file reading

        public async Task<HashResult> CalculateFileHashAsync(string filePath, HashType hashType, IProgress<int>? progress = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("文件不存在", filePath);
            }

            var startTime = DateTime.Now;
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            using var hashAlgorithm = CreateHashAlgorithm(hashType);

            var buffer = new byte[BufferSize];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, BufferSize)) > 0)
            {
                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                totalBytesRead += bytesRead;

                // Report progress (0-100)
                progress?.Report(fileSize > 0 ? (int)((totalBytesRead * 100) / fileSize) : 0);
            }

            // Complete the hash computation
            hashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var hashBytes = hashAlgorithm.Hash ?? Array.Empty<byte>();
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

            return new HashResult
            {
                Hash = hashString,
                Algorithm = hashType.ToString(),
                FileSize = fileSize,
                CalculationTime = DateTime.Now - startTime
            };
        }

        public async Task<HashResult> CalculateTextHashAsync(string text, HashType hashType)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("文本不能为空", nameof(text));
            }

            var startTime = DateTime.Now;

            using var hashAlgorithm = CreateHashAlgorithm(hashType);
            var textBytes = Encoding.UTF8.GetBytes(text);
            var hashBytes = await Task.Run(() => hashAlgorithm.ComputeHash(textBytes));

            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

            return new HashResult
            {
                Hash = hashString,
                Algorithm = hashType.ToString(),
                FileSize = textBytes.Length,
                CalculationTime = DateTime.Now - startTime
            };
        }

        public async Task<HashResult[]> CalculateMultipleHashesAsync(string filePath, HashType[] hashTypes, IProgress<int>? progress = null)
        {
            var results = new List<HashResult>();
            var fileSize = new FileInfo(filePath).Length;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            var hashAlgorithms = hashTypes.Select(CreateHashAlgorithm).ToArray();

            var buffer = new byte[BufferSize];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, BufferSize)) > 0)
            {
                foreach (var algorithm in hashAlgorithms)
                {
                    algorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                totalBytesRead += bytesRead;

                progress?.Report(fileSize > 0 ? (int)((totalBytesRead * 100) / fileSize) : 0);
            }

            for (int i = 0; i < hashAlgorithms.Length; i++)
            {
                var startTime = DateTime.Now;
                hashAlgorithms[i].TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var hashBytes = hashAlgorithms[i].Hash ?? Array.Empty<byte>();
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();

                results.Add(new HashResult
                {
                    Hash = hashString,
                    Algorithm = hashAlgorithms[i].GetType().Name.Replace("Managed", "").ToUpperInvariant(),
                    FileSize = fileSize,
                    CalculationTime = DateTime.Now - startTime
                });
            }

            return results.ToArray();
        }

        private static HashAlgorithm CreateHashAlgorithm(HashType hashType)
        {
            return hashType switch
            {
                HashType.MD5 => MD5.Create(),
                HashType.SHA1 => SHA1.Create(),
                HashType.SHA256 => SHA256.Create(),
                HashType.SHA384 => SHA384.Create(),
                HashType.SHA512 => SHA512.Create(),
                _ => throw new ArgumentException($"不支持的哈希算法: {hashType}")
            };
        }

        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }

        public static bool IsValidFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }
    }
}