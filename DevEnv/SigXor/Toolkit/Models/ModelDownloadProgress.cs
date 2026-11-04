namespace DevEnv.Host.SigXor.Toolkit.Models;

public readonly record struct ModelDownloadProgress(int Percent, string Message)
{
    public static ModelDownloadProgress Indeterminate(string message) => new(-1, message);
}
