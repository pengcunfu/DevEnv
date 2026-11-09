using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using DevEnv.Core.Components;

namespace DevEnv.Tests;

public sealed class ComponentInstallerTests
{
    [Fact]
    public async Task MatchesSha256_ReturnsTrueForMatchingContent()
    {
        string directory = CreateTempDirectory();
        try
        {
            string file = Path.Combine(directory, "payload.bin");
            await File.WriteAllBytesAsync(file, [1, 2, 3, 4, 5]);
            string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant();

            Assert.True(await ComponentInstaller.MatchesSha256Async(file, hash));
            Assert.False(await ComponentInstaller.MatchesSha256Async(file, new string('0', 64)));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task InstallAsync_DownloadsAndExtractsComponent()
    {
        string directory = CreateTempDirectory();
        try
        {
            byte[] zip = CreateZip(("DocumentFormat.OpenXml.dll", "fake-openxml"));
            string sha = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
            string manifest = $$"""
            {
              "version": 1,
              "components": [
                { "id": "office", "name": "Office 预览", "version": "1.0.0",
                  "size": {{zip.Length}}, "sha256": "{{sha}}",
                  "urls": ["https://example.com/office.zip"] }
              ]
            }
            """;

            var store = new ComponentStore(Path.Combine(directory, "components"));
            var http = new HttpClient(new FakeHandler(manifest, zip));
            var installer = new ComponentInstaller(store, http);

            await installer.InstallAsync(ComponentCatalog.Office);

            Assert.True(store.IsInstalled(ComponentCatalog.Office));
            string? componentDirectory = store.GetDirectory(ComponentCatalog.Office);
            Assert.NotNull(componentDirectory);
            Assert.Equal("fake-openxml",
                File.ReadAllText(Path.Combine(componentDirectory!, "DocumentFormat.OpenXml.dll")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ExtractZipSafe_ExtractsNormalEntries()
    {
        string directory = CreateTempDirectory();
        try
        {
            string zip = Path.Combine(directory, "component.zip");
            using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("nested/file.txt");
                using StreamWriter writer = new(entry.Open());
                writer.Write("hello");
            }

            string destination = Path.Combine(directory, "extracted");
            ComponentInstaller.ExtractZipSafe(zip, destination);

            Assert.Equal("hello", File.ReadAllText(Path.Combine(destination, "nested", "file.txt")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void ExtractZipSafe_RejectsPathTraversal()
    {
        string directory = CreateTempDirectory();
        try
        {
            string zip = Path.Combine(directory, "evil.zip");
            using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("../escaped.txt");
                using StreamWriter writer = new(entry.Open());
                writer.Write("escaped");
            }

            string destination = Path.Combine(directory, "extracted");
            Assert.Throws<InvalidDataException>(() => ComponentInstaller.ExtractZipSafe(zip, destination));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _manifest;
        private readonly byte[] _zip;

        public FakeHandler(string manifest, byte[] zip)
        {
            _manifest = manifest;
            _zip = zip;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = request.RequestUri?.AbsolutePath.EndsWith("components.json", StringComparison.OrdinalIgnoreCase) == true
                ? new StringContent(_manifest, Encoding.UTF8, "application/json")
                : new ByteArrayContent(_zip);
            return Task.FromResult(response);
        }
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "devenv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
        }
    }
}
