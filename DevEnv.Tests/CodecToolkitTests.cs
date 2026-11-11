using System.Globalization;
using DevEnv.Core.Codec;

namespace DevEnv.Tests;

public sealed class CodecToolkitTests
{
    [Fact]
    public void Base64_RoundTripsUtf8()
    {
        var encoded = CodecToolkit.Base64Encode("你好 FireNeb", "UTF-8");
        Assert.True(encoded.Ok);
        var decoded = CodecToolkit.Base64Decode(encoded.Text, "UTF-8");
        Assert.True(decoded.Ok);
        Assert.Equal("你好 FireNeb", decoded.Text);
    }

    [Fact]
    public void Url_RoundTrips()
    {
        var encoded = CodecToolkit.UrlEncode("a b&c=1");
        Assert.True(encoded.Ok);
        Assert.Contains("%20", encoded.Text, StringComparison.Ordinal);
        var decoded = CodecToolkit.UrlDecode(encoded.Text);
        Assert.True(decoded.Ok);
        Assert.Equal("a b&c=1", decoded.Text);
    }

    [Fact]
    public void Hex_RoundTrips()
    {
        var encoded = CodecToolkit.HexEncode("ABC", "ASCII");
        Assert.True(encoded.Ok);
        Assert.Equal("414243", encoded.Text);
        var decoded = CodecToolkit.HexDecode("41 42-43", "ASCII");
        Assert.True(decoded.Ok);
        Assert.Equal("ABC", decoded.Text);
    }

    [Fact]
    public void Unicode_RoundTrips()
    {
        var encoded = CodecToolkit.UnicodeEncode("测A");
        Assert.True(encoded.Ok);
        Assert.StartsWith("\\u", encoded.Text, StringComparison.Ordinal);
        var decoded = CodecToolkit.UnicodeDecode(encoded.Text);
        Assert.True(decoded.Ok);
        Assert.Equal("测A", decoded.Text);
    }

    [Fact]
    public void Html_RoundTrips()
    {
        var encoded = CodecToolkit.HtmlEncode("<a>&");
        Assert.True(encoded.Ok);
        Assert.Contains("&lt;", encoded.Text, StringComparison.Ordinal);
        var decoded = CodecToolkit.HtmlDecode(encoded.Text);
        Assert.True(decoded.Ok);
        Assert.Equal("<a>&", decoded.Text);
    }

    [Fact]
    public void HashText_Md5_IsStable()
    {
        using var _ = new InvariantCultureScope();
        var result = CodecToolkit.HashText("abc", "MD5", "ASCII");
        Assert.True(result.Ok);
        Assert.Equal("900150983CD24FB0D6963F7D28E17F72", result.Text);
    }

    [Fact]
    public void Aes_RoundTrips()
    {
        var encrypted = CodecToolkit.AesEncrypt("secret payload", "pass-phrase", "UTF-8");
        Assert.True(encrypted.Ok);
        var decrypted = CodecToolkit.AesDecrypt(encrypted.Text, "pass-phrase", "UTF-8");
        Assert.True(decrypted.Ok);
        Assert.Equal("secret payload", decrypted.Text);
    }

    [Fact]
    public void AesDecrypt_WrongPassword_Fails()
    {
        var encrypted = CodecToolkit.AesEncrypt("secret", "right", "UTF-8");
        Assert.True(encrypted.Ok);
        var decrypted = CodecToolkit.AesDecrypt(encrypted.Text, "wrong", "UTF-8");
        Assert.False(decrypted.Ok);
    }

    [Fact]
    public void Xor_RoundTripsViaHex()
    {
        var encrypted = CodecToolkit.XorCrypt("hello", "key", "ASCII");
        Assert.True(encrypted.Ok);
        var decrypted = CodecToolkit.XorDecryptHex(encrypted.Text, "key", "ASCII");
        Assert.True(decrypted.Ok);
        Assert.Equal("hello", decrypted.Text);
    }

    [Fact]
    public void UnixToDateTime_ParsesSeconds()
    {
        using var _ = new InvariantCultureScope();
        var result = CodecToolkit.UnixToDateTime("0", milliseconds: false);
        Assert.True(result.Ok);
        Assert.Contains("秒：0", result.Text, StringComparison.Ordinal);
        Assert.Contains("毫秒：0", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DateTimeToUnix_RejectsGarbage()
    {
        var result = CodecToolkit.DateTimeToUnix("not-a-date", milliseconds: false);
        Assert.False(result.Ok);
    }

    [Fact]
    public void NowTimestamps_ContainsFields()
    {
        var result = CodecToolkit.NowTimestamps();
        Assert.True(result.Ok);
        Assert.Contains("本地：", result.Text, StringComparison.Ordinal);
        Assert.Contains("秒：", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Base64Decode_Invalid_Fails()
    {
        var result = CodecToolkit.Base64Decode("%%%");
        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
