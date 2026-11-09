using DevEnv.Core.Components;

namespace DevEnv.Tests;

public sealed class ComponentStoreTests
{
    [Fact]
    public void RecordInstall_ThenPersistsAndDetectsMissingKeyFile()
    {
        string root = CreateTempDirectory();
        try
        {
            string installDirectory = Path.Combine(root, "office", "1.3.0");
            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(Path.Combine(installDirectory, "DocumentFormat.OpenXml.dll"), "");

            var store = new ComponentStore(root);
            Assert.False(store.IsInstalled(ComponentCatalog.Office));

            store.RecordInstall(ComponentCatalog.Office, "1.3.0", new string('a', 64), installDirectory);
            Assert.True(store.IsInstalled(ComponentCatalog.Office));
            Assert.Equal("1.3.0", store.GetInstalledVersion(ComponentCatalog.Office));

            File.Delete(Path.Combine(installDirectory, "DocumentFormat.OpenXml.dll"));
            Assert.False(store.IsInstalled(ComponentCatalog.Office));

            var reloaded = new ComponentStore(root);
            Assert.Equal("1.3.0", reloaded.GetRecord(ComponentCatalog.Office)!.Version);
            Assert.False(reloaded.IsInstalled(ComponentCatalog.Office));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Remove_ClearsState()
    {
        string root = CreateTempDirectory();
        try
        {
            string installDirectory = Path.Combine(root, "voice", "1.3.0");
            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(Path.Combine(installDirectory, "sherpa-onnx-c-api.dll"), "");
            File.WriteAllText(Path.Combine(installDirectory, "onnxruntime.dll"), "");

            var store = new ComponentStore(root);
            store.RecordInstall(ComponentCatalog.Voice, "1.3.0", new string('b', 64), installDirectory);
            Assert.True(store.IsInstalled(ComponentCatalog.Voice));

            Assert.True(store.Remove(ComponentCatalog.Voice));
            Assert.False(store.IsInstalled(ComponentCatalog.Voice));
        }
        finally
        {
            DeleteDirectory(root);
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
