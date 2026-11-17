namespace DevEnv.Core.SystemInfo.Models;

public sealed record SystemInfoItem(string Name, string Value);

public sealed record SystemInfoGroup(string Title, IReadOnlyList<SystemInfoItem> Items);
