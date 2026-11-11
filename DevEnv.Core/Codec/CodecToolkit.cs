using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DevEnv.Core.Codec;

/// <summary>常用编解码 / 加解密 / 哈希 / 时间转换的纯函数集合。</summary>
public static class CodecToolkit
{
    public static readonly string[] TextEncodings = ["UTF-8", "UTF-16LE", "UTF-16BE", "GBK", "ASCII"];

    public static readonly string[] HashAlgorithms = ["MD5", "SHA1", "SHA256", "SHA384", "SHA512"];

    public static Encoding ResolveEncoding(string? name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return (name?.Trim().ToUpperInvariant()) switch
        {
            "UTF-16" or "UTF-16LE" or "UNICODE" => Encoding.Unicode,
            "UTF-16BE" => Encoding.BigEndianUnicode,
            "ASCII" or "US-ASCII" => Encoding.ASCII,
            "GBK" or "GB2312" or "GB18030" => Encoding.GetEncoding(936),
            _ => Encoding.UTF8,
        };
    }

    public static CodecResult Base64Encode(string text, string? encodingName = null)
    {
        try
        {
            var bytes = ResolveEncoding(encodingName).GetBytes(text ?? string.Empty);
            return CodecResult.Success(Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult Base64Decode(string text, string? encodingName = null)
    {
        try
        {
            var raw = (text ?? string.Empty).Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
            var bytes = Convert.FromBase64String(raw);
            return CodecResult.Success(ResolveEncoding(encodingName).GetString(bytes));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"Base64 解码失败：{ex.Message}");
        }
    }

    public static CodecResult Base64EncodeBytes(byte[] bytes)
    {
        try
        {
            return CodecResult.Success(Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult Base64DecodeToBytes(string text)
    {
        try
        {
            var raw = (text ?? string.Empty).Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "");
            return CodecResult.FromBytes(Convert.FromBase64String(raw));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"Base64 解码失败：{ex.Message}");
        }
    }

    public static CodecResult UrlEncode(string text) =>
        CodecResult.Success(Uri.EscapeDataString(text ?? string.Empty));

    public static CodecResult UrlDecode(string text)
    {
        try
        {
            return CodecResult.Success(Uri.UnescapeDataString((text ?? string.Empty).Replace("+", "%20")));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"URL 解码失败：{ex.Message}");
        }
    }

    public static CodecResult HexEncode(string text, string? encodingName = null)
    {
        try
        {
            var bytes = ResolveEncoding(encodingName).GetBytes(text ?? string.Empty);
            return CodecResult.Success(Convert.ToHexString(bytes));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult HexDecode(string text, string? encodingName = null)
    {
        try
        {
            var raw = Regex.Replace(text ?? string.Empty, @"[\s\-:,]", "");
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                raw = raw[2..];
            if (raw.Length % 2 != 0)
                return CodecResult.Fail("十六进制长度必须为偶数");

            var bytes = Convert.FromHexString(raw);
            return CodecResult.Success(ResolveEncoding(encodingName).GetString(bytes));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"Hex 解码失败：{ex.Message}");
        }
    }

    public static CodecResult UnicodeEncode(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text ?? string.Empty)
            sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
        return CodecResult.Success(sb.ToString());
    }

    public static CodecResult UnicodeDecode(string text)
    {
        try
        {
            var result = Regex.Replace(
                text ?? string.Empty,
                @"\\u([0-9a-fA-F]{4})",
                m => char.ConvertFromUtf32(int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            return CodecResult.Success(result);
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"Unicode 解码失败：{ex.Message}");
        }
    }

    public static CodecResult HtmlEncode(string text) =>
        CodecResult.Success(WebUtility.HtmlEncode(text ?? string.Empty));

    public static CodecResult HtmlDecode(string text) =>
        CodecResult.Success(WebUtility.HtmlDecode(text ?? string.Empty));

    public static CodecResult HashText(string text, string algorithm, string? encodingName = null)
    {
        try
        {
            var bytes = ResolveEncoding(encodingName).GetBytes(text ?? string.Empty);
            using var hasher = CreateHash(algorithm);
            var hash = hasher.ComputeHash(bytes);
            return CodecResult.Success(Convert.ToHexString(hash));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult AesEncrypt(string text, string password, string? encodingName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
                return CodecResult.Fail("请填写密钥");

            var plain = ResolveEncoding(encodingName).GetBytes(text ?? string.Empty);
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var iv = RandomNumberGenerator.GetBytes(16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            var payload = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, payload, iv.Length, cipher.Length);
            return CodecResult.Success(Convert.ToBase64String(payload));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"AES 加密失败：{ex.Message}");
        }
    }

    public static CodecResult AesDecrypt(string text, string password, string? encodingName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
                return CodecResult.Fail("请填写密钥");

            var payload = Convert.FromBase64String((text ?? string.Empty).Trim());
            if (payload.Length <= 16)
                return CodecResult.Fail("密文过短");

            var iv = payload.AsSpan(0, 16).ToArray();
            var cipher = payload.AsSpan(16).ToArray();
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return CodecResult.Success(ResolveEncoding(encodingName).GetString(plain));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"AES 解密失败：{ex.Message}");
        }
    }

    public static CodecResult XorCrypt(string text, string password, string? encodingName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
                return CodecResult.Fail("请填写密钥");

            var encoding = ResolveEncoding(encodingName);
            var data = encoding.GetBytes(text ?? string.Empty);
            var key = encoding.GetBytes(password);
            for (var i = 0; i < data.Length; i++)
                data[i] ^= key[i % key.Length];
            return CodecResult.Success(Convert.ToHexString(data));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult XorDecryptHex(string hex, string password, string? encodingName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(password))
                return CodecResult.Fail("请填写密钥");

            var raw = Regex.Replace(hex ?? string.Empty, @"[\s\-]", "");
            var data = Convert.FromHexString(raw);
            var encoding = ResolveEncoding(encodingName);
            var key = encoding.GetBytes(password);
            for (var i = 0; i < data.Length; i++)
                data[i] ^= key[i % key.Length];
            return CodecResult.Success(encoding.GetString(data));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail($"XOR 解密失败：{ex.Message}");
        }
    }

    public static CodecResult UnixToDateTime(string text, bool milliseconds)
    {
        try
        {
            if (!long.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return CodecResult.Fail("请输入有效的 Unix 时间戳");

            var dto = milliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);
            var local = dto.ToLocalTime();
            var utc = dto.ToUniversalTime();
            return CodecResult.Success(
                $"本地：{local:yyyy-MM-dd HH:mm:ss.fff} ({local:zzz})\n"
                + $"UTC：{utc:yyyy-MM-dd HH:mm:ss.fff} Z\n"
                + $"秒：{dto.ToUnixTimeSeconds()}\n"
                + $"毫秒：{dto.ToUnixTimeMilliseconds()}");
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult DateTimeToUnix(string text, bool milliseconds)
    {
        try
        {
            var raw = (text ?? string.Empty).Trim();
            if (!DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var local)
                && !DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out local))
            {
                return CodecResult.Fail("请输入可解析的时间，例如 2026-11-11 10:00:00");
            }

            if (local.Kind == DateTimeKind.Unspecified)
                local = DateTime.SpecifyKind(local, DateTimeKind.Local);

            var dto = new DateTimeOffset(local.ToLocalTime());
            return CodecResult.Success(milliseconds
                ? dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                : dto.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            return CodecResult.Fail(ex.Message);
        }
    }

    public static CodecResult NowTimestamps()
    {
        var now = DateTimeOffset.Now;
        return CodecResult.Success(
            $"本地：{now:yyyy-MM-dd HH:mm:ss.fff} ({now:zzz})\n"
            + $"UTC：{now.ToUniversalTime():yyyy-MM-dd HH:mm:ss.fff} Z\n"
            + $"秒：{now.ToUnixTimeSeconds()}\n"
            + $"毫秒：{now.ToUnixTimeMilliseconds()}");
    }

    private static HashAlgorithm CreateHash(string algorithm) =>
        (algorithm?.Trim().ToUpperInvariant()) switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => throw new ArgumentException($"不支持的哈希算法：{algorithm}"),
        };
}

/// <summary>编解码结果：文本或字节。</summary>
public sealed class CodecResult
{
    public bool Ok { get; init; }
    public string Text { get; init; } = string.Empty;
    public byte[]? Bytes { get; init; }
    public string Error { get; init; } = string.Empty;

    public static CodecResult Success(string text) => new() { Ok = true, Text = text };
    public static CodecResult FromBytes(byte[] bytes) => new() { Ok = true, Bytes = bytes, Text = Convert.ToBase64String(bytes) };
    public static CodecResult Fail(string error) => new() { Ok = false, Error = error };
}
