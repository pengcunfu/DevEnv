using DevEnv.Core.Clipboard.Models;
using DevEnv.Core.Clipboard.Services;
using Microsoft.Data.Sqlite;

namespace DevEnv.Tests;

public sealed class HistoryStoreTests
{
    [Fact]
    public void DeleteRange_RemovesOnlyRequestedDayAndType()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devenv-clip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "history.db");

        try
        {
            using (var store = new HistoryStore(dbPath))
            {
                store.Open();
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-01 09:00:00", Type = "text", Text = "d1" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-02 09:00:00", Type = "text", Text = "d2" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-02 10:00:00", Type = "image", ImagePath = "b.png" });

                Assert.Equal(1, store.DeleteRange("2026-01-02 00:00:00", "2026-01-03 00:00:00", "image"));
                Assert.Equal(2, store.Count());
                Assert.Equal(2, store.Count(new HistoryQuery(Type: "text")));
                Assert.Equal(0, store.Count(new HistoryQuery(Type: "image")));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响断言结果。
            }
        }
    }

    [Fact]
    public void CountByType_GroupsRowsByType()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devenv-clip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "history.db");

        try
        {
            using (var store = new HistoryStore(dbPath))
            {
                store.Open();
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:00:00", Type = "text", Text = "a" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:01:00", Type = "text", Text = "b" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:02:00", Type = "image", ImagePath = "a.png" });

                var counts = store.CountByType("2026-01-01 00:00:00");

                Assert.Equal(2, counts["text"]);
                Assert.Equal(1, counts["image"]);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响断言结果。
            }
        }
    }

    [Fact]
    public void DeleteAll_WithType_RemovesOnlyThatType()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devenv-clip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "history.db");

        try
        {
            using (var store = new HistoryStore(dbPath))
            {
                store.Open();
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:00:00", Type = "text", Text = "text" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:01:00", Type = "image", ImagePath = "a.png" });

                Assert.Equal(1, store.DeleteAll("image"));
                Assert.Equal(1, store.Count());
                Assert.Single(store.Query(new HistoryQuery(Type: "text")));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响断言结果。
            }
        }
    }

    [Fact]
    public void DeleteBefore_RemovesOnlyExpiredRows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "devenv-clip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "history.db");

        try
        {
            using (var store = new HistoryStore(dbPath))
            {
                store.Open();
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-01 08:00:00", Type = "text", Text = "old" });
                store.Insert(new ClipboardEntry { Timestamp = "2026-01-05 08:00:00", Type = "text", Text = "new" });

                var cutoff = "2026-01-03 00:00:00";
                var expired = store.Query(new HistoryQuery(UntilTimestamp: cutoff));

                Assert.Single(expired);
                Assert.Equal("old", expired[0].Text);
                Assert.Equal(1, store.Count(new HistoryQuery(UntilTimestamp: cutoff)));

                Assert.Equal(1, store.DeleteBefore(cutoff));
                Assert.Equal(1, store.Count());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响断言结果。
            }
        }
    }
}
