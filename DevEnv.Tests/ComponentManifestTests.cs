using DevEnv.Core.Components;

namespace DevEnv.Tests;

public sealed class ComponentManifestTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Parse_ReadsAllComponentsAndUrls()
    {
        string json = $$"""
        {
          "version": 1,
          "appVersion": "1.3.0",
          "components": [
            { "id": "office", "name": "Office 预览", "version": "1.3.0", "size": 8900000,
              "sha256": "{{ValidSha}}",
              "urls": ["https://github.com/pengcunfu/DevEnv/releases/download/v1.3.0/office.zip"] },
            { "id": "ocr", "name": "OCR 引擎", "version": "1.3.0", "size": 30000000,
              "sha256": "{{ValidSha}}", "url": "https://example.com/ocr.zip" },
            { "id": "voice", "name": "语音引擎", "version": "1.3.0", "size": 21000000,
              "sha256": "{{ValidSha}}", "urls": ["https://example.com/voice.zip"] }
          ]
        }
        """;

        ComponentManifest manifest = ComponentManifest.Parse(json);

        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("1.3.0", manifest.AppVersion);
        Assert.Equal(3, manifest.Components.Count);
        Assert.Equal(8_900_000, manifest.Find("Office")!.Size);
        Assert.Single(manifest.Find("ocr")!.Urls);
        Assert.Equal("https://example.com/ocr.zip", manifest.Find("ocr")!.Urls[0]);
    }

    [Fact]
    public void Parse_DropsInvalidEntries()
    {
        string json = $$"""
        {
          "version": 1,
          "components": [
            { "id": "office", "urls": ["https://example.com/office.zip"] },
            { "id": "ocr", "sha256": "{{ValidSha}}", "urls": [] },
            { "id": "voice", "sha256": "{{ValidSha}}", "urls": ["https://example.com/voice.zip"] }
          ]
        }
        """;

        ComponentManifest manifest = ComponentManifest.Parse(json);

        Assert.Single(manifest.Components);
        Assert.Equal("voice", manifest.Components[0].Id);
    }

    [Fact]
    public void Parse_RejectsNewerFormat()
    {
        string json = $$"""
        { "version": 99, "components": [] }
        """;

        Assert.Throws<InvalidOperationException>(() => ComponentManifest.Parse(json));
    }

    [Fact]
    public void Parse_AllowsCommentsAndTrailingCommas()
    {
        string json = $$"""
        {
          // components
          "version": 1,
          "components": [
            { "id": "office", "sha256": "{{ValidSha}}", "urls": ["https://example.com/office.zip"] },
          ],
        }
        """;

        ComponentManifest manifest = ComponentManifest.Parse(json);
        Assert.Single(manifest.Components);
    }
}
