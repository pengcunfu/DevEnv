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

    [Fact]
    public void DevOutput_NativeKeyFilesUnderRuntimes_AreDetected()
    {
        // 框架依赖构建（make run 的 Debug 输出）把原生库放在 runtimes\<rid>\native，
        // 不能因为 exe 旁边没有就直接判定「组件未安装」。
        string root = CreateTempDirectory();
        string baseDirectory = CreateTempDirectory();
        try
        {
            string native = Path.Combine(baseDirectory, "runtimes", $"win-{ArchSuffix}", "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(native, "sherpa-onnx-c-api.dll"), "");
            File.WriteAllText(Path.Combine(native, "onnxruntime.dll"), "");

            var store = new ComponentStore(root, baseDirectory);
            Assert.True(store.IsInstalled(ComponentCatalog.Voice));
            Assert.Equal(Path.GetFullPath(baseDirectory), store.GetDirectory(ComponentCatalog.Voice));
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void DevOutput_MixedManagedAndNativeKeyFiles_AreDetected()
    {
        // OCR 的 RapidOcrNet.dll 在输出目录根部，libSkiaSharp / onnxruntime 在 runtimes 下。
        string root = CreateTempDirectory();
        string baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, "RapidOcrNet.dll"), "");
            string native = Path.Combine(baseDirectory, "runtimes", $"win-{ArchSuffix}", "native");
            Directory.CreateDirectory(native);
            foreach (string file in ComponentCatalog.OcrEngine.KeyFiles)
            {
                if (file == "RapidOcrNet.dll") continue;
                File.WriteAllText(Path.Combine(native, file), "");
            }

            var store = new ComponentStore(root, baseDirectory);
            Assert.True(store.IsInstalled(ComponentCatalog.Ocr));
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(baseDirectory);
        }
    }

    [Fact]
    public void DevOutput_MissingNativeKeyFile_IsNotInstalled()
    {
        string root = CreateTempDirectory();
        string baseDirectory = CreateTempDirectory();
        try
        {
            string native = Path.Combine(baseDirectory, "runtimes", $"win-{ArchSuffix}", "native");
            Directory.CreateDirectory(native);
            File.WriteAllText(Path.Combine(native, "sherpa-onnx-c-api.dll"), "");

            var store = new ComponentStore(root, baseDirectory);
            Assert.False(store.IsInstalled(ComponentCatalog.Voice));
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(baseDirectory);
        }
    }

    private static string ArchSuffix => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        System.Runtime.InteropServices.Architecture.Arm => "arm",
        _ => "x64",
    };

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
