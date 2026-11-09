namespace DevEnv.Core.Updates;

/// <summary>解析更新器重启时携带的参数，供 App 提示“已更新 / 已回滚”。</summary>
public static class UpdateStartupHooks
{
    public static string? UpdatedTo { get; private set; }
    public static string? RolledBackFrom { get; private set; }
    public static string? RolledBackTo { get; private set; }
    public static bool ManualRollback { get; private set; }

    public static void ParseArgs(string[] args)
    {
        UpdatedTo = GetArg(args, "--updated-to");
        RolledBackFrom = GetArg(args, "--rollback-from");
        RolledBackTo = GetArg(args, "--rollback-to");
        ManualRollback = args.Contains("--manual-rollback", StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetArg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
