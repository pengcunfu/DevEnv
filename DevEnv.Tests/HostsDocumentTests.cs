using DevEnv.Core.LavaEnv.Models;
using DevEnv.Core.LavaEnv.Services;

namespace DevEnv.Tests;

public sealed class HostsDocumentTests
{
    [Fact]
    public void ParseEntries_SkipsCommentsAndBlankLines()
    {
        var entries = HostsDocument.ParseEntries("""
            # comment

            127.0.0.1 localhost
            ::1 localhost
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal(3, entries[0].LineNumber);
        Assert.Equal("127.0.0.1", entries[0].Ip);
        Assert.Equal(4, entries[1].LineNumber);
        Assert.Equal("::1", entries[1].Ip);
    }

    [Fact]
    public void ApplyEntries_PreservesComments_UpdatesAndAppends()
    {
        const string original = """
            # keep me
            127.0.0.1 localhost
            10.0.0.1 old.local
            """;

        var entries = HostsDocument.ParseEntries(original).ToList();
        entries[0].Domain = "localhost local.dev";
        entries.RemoveAt(1); // delete 10.0.0.1
        entries.Add(new HostsEntry { Ip = "192.168.1.1", Domain = "nas.local", LineNumber = 0 });

        var text = HostsDocument.ApplyEntries(original, entries).Replace("\r\n", "\n");
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("# keep me", lines[0]);
        Assert.Equal("127.0.0.1\tlocalhost local.dev", lines[1]);
        Assert.Equal("192.168.1.1\tnas.local", lines[2]);
        Assert.DoesNotContain(lines, line => line.Contains("old.local", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyEntries_RoundTripsParsedEntries()
    {
        const string original = """
            # header
            127.0.0.1	localhost
            """;

        var before = HostsDocument.ParseEntries(original);
        var text = HostsDocument.ApplyEntries(original, before);
        var after = HostsDocument.ParseEntries(text);

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before[0].Ip, after[0].Ip);
        Assert.Equal(before[0].Domain, after[0].Domain);
    }
}
