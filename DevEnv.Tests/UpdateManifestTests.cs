using DevEnv.Core.Updates;

namespace DevEnv.Tests;

public sealed class UpdateManifestTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Parse_ReturnsManifestFields()
    {
        string json = $$"""
        {
          "version": "1.3.0",
          "url": "https://github.com/pengcunfu/DevEnv/releases/download/v1.3.0/DevEnv-1.3.0-win-x64.zip",
          "sha256": "{{ValidSha}}",
          "size": 52000000
        }
        """;

        UpdateManifest manifest = UpdateManifestParser.Parse(json);

        Assert.Equal("1.3.0", manifest.Version);
        Assert.Equal(ValidSha, manifest.Sha256);
        Assert.Equal(52_000_000, manifest.Size);
        Assert.EndsWith(".zip", manifest.Url);
    }

    [Fact]
    public void Parse_AllowsCommentsAndTrailingCommas()
    {
        string json = $$"""
        {
          // version
          "version": "1.3.0",
          "url": "https://example.com/app.zip",
          "sha256": "{{ValidSha}}",
        }
        """;

        UpdateManifest manifest = UpdateManifestParser.Parse(json);
        Assert.Equal("1.3.0", manifest.Version);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"version":"1.3.0","url":"https://example.com/app.zip"}""")]
    [InlineData("""{"url":"https://example.com/app.zip","sha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    [InlineData("""{"version":"not-a-version","url":"https://example.com/app.zip","sha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""")]
    [InlineData("not json")]
    public void Parse_RejectsInvalidManifest(string json)
    {
        Assert.Throws<InvalidOperationException>(() => UpdateManifestParser.Parse(json));
    }

    [Fact]
    public void IsNewerThan_ComparesNumericVersions()
    {
        var manifest = new UpdateManifest("1.3.0", "https://example.com/app.zip", ValidSha, 0);

        Assert.True(manifest.IsNewerThan(new Version(1, 2, 0)));
        Assert.False(manifest.IsNewerThan(new Version(1, 3, 0)));
        Assert.False(manifest.IsNewerThan(new Version(1, 4, 0)));
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3.0", "1.2.3")]
    [InlineData("1.2.3-beta", "1.2.3")]
    public void AppVersions_ParsesCommonForms(string text, string expectedDisplay)
    {
        Assert.True(AppVersions.TryParse(text, out Version version));
        Assert.Equal(expectedDisplay, AppVersions.Display(version));
    }

    [Fact]
    public void AppVersions_RejectsInvalidText()
    {
        Assert.False(AppVersions.TryParse(null, out _));
        Assert.False(AppVersions.TryParse("", out _));
        Assert.False(AppVersions.TryParse("abc", out _));
    }
}
