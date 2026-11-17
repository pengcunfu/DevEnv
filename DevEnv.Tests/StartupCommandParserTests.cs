using DevEnv.Core.Services;

namespace DevEnv.Tests;

public sealed class StartupCommandParserTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\App\app.exe"" --startup", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe", @"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe -x", @"C:\Windows\System32\notepad.exe")]
    [InlineData(@"  ""C:\Program Files\App\app.exe""  ", @"C:\Program Files\App\app.exe")]
    [InlineData("rundll32.exe shell32.dll,Control_RunDLL", "rundll32.exe")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ExtractExecutablePath_HandlesQuotesAndArguments(string command, string expected)
    {
        Assert.Equal(expected, StartupCommandParser.ExtractExecutablePath(command));
    }

    [Fact]
    public void ExtractExecutablePath_ExpandsEnvironmentVariables()
    {
        var path = StartupCommandParser.ExtractExecutablePath(@"%SystemRoot%\System32\notepad.exe");

        Assert.DoesNotContain("%", path);
        Assert.EndsWith(@"System32\notepad.exe", path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("rundll32.exe", true)]
    [InlineData("rundll32", true)]
    [InlineData(@"C:\App\app.exe", false)]
    [InlineData(@"bin\app.exe", false)]
    [InlineData("", false)]
    public void IsBareCommandName_DetectsNamesWithoutDirectory(string path, bool expected)
    {
        Assert.Equal(expected, StartupCommandParser.IsBareCommandName(path));
    }

    [Fact]
    public void GetFileState_ReportsExistingAndMissingFiles()
    {
        var existing = Path.GetTempFileName();
        try
        {
            Assert.Equal(StartupFileState.Exists, StartupCommandParser.GetFileState(existing));
        }
        finally
        {
            File.Delete(existing);
        }

        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        Assert.Equal(StartupFileState.Missing, StartupCommandParser.GetFileState(missing));
        Assert.Equal(StartupFileState.Unresolved, StartupCommandParser.GetFileState("rundll32.exe"));
        Assert.Equal(StartupFileState.Unknown, StartupCommandParser.GetFileState(""));
    }
}
