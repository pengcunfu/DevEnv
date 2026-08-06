using System.Reflection;

namespace ProgramBox.Utils
{
    public static class VersionInfo
    {
        public const string DocumentationUrl = "https://pengcunfu.github.io/devbox/";
        public const string RepositoryUrl = "https://github.com/pengcunfu/devbox";
        public const string ReleasesUrl = "https://github.com/pengcunfu/devbox/releases";

        public static string Current =>
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "1.0.0";

        public static string Display => $"v{Current}";
    }
}
