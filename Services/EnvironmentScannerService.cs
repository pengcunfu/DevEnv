using System.Diagnostics;
using System.IO;
using DevEnv.Models;
using Microsoft.Win32;

namespace DevEnv.Services
{
    public class EnvironmentScannerService
    {
        public async Task<List<InstalledEnvironment>> ScanAllAsync()
        {
            var results = new List<InstalledEnvironment>();
            results.AddRange(await ScanJavaAsync());
            results.AddRange(await ScanPythonAsync());
            results.AddRange(await ScanNodeJsAsync());
            results.AddRange(ScanPhp());
            results.AddRange(await ScanDotNetAsync());
            results.AddRange(await ScanGitAsync());
            results.AddRange(await ScanMavenAsync());
            results.AddRange(await ScanGoAsync());
            return results.OrderBy(r => r.Type).ThenBy(r => r.Name).ToList();
        }

        public async Task<List<InstalledEnvironment>> ScanJavaAsync()
        {
            var results = new List<InstalledEnvironment>();
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
            {
                var version = await GetCommandVersionAsync(Path.Combine(javaHome, "bin", "java.exe"), "-version");
                results.Add(new InstalledEnvironment
                {
                    Type = "Java",
                    Name = "JAVA_HOME",
                    Version = version,
                    Path = javaHome,
                    IsInPath = true
                });
            }

            var (success, output) = await RunCommandAsync("java", "-version");
            if (success)
            {
                var javaPath = await FindExecutablePathAsync("java");
                results.Add(new InstalledEnvironment
                {
                    Type = "Java",
                    Name = "java (PATH)",
                    Version = ExtractVersion(output),
                    Path = javaPath ?? "PATH",
                    IsInPath = true
                });
            }

            ScanRegistryJava(results);
            return DeduplicateByPath(results);
        }

        public async Task<List<InstalledEnvironment>> ScanPythonAsync()
        {
            var results = new List<InstalledEnvironment>();
            foreach (var cmd in new[] { "python", "python3", "py" })
            {
                var (success, output) = await RunCommandAsync(cmd, "--version");
                if (success)
                {
                    var path = await FindExecutablePathAsync(cmd);
                    results.Add(new InstalledEnvironment
                    {
                        Type = "Python",
                        Name = cmd,
                        Version = output.Trim(),
                        Path = path ?? "PATH",
                        IsInPath = true
                    });
                }
            }

            var pythonHome = Environment.GetEnvironmentVariable("PYTHONHOME");
            if (!string.IsNullOrEmpty(pythonHome))
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Python",
                    Name = "PYTHONHOME",
                    Version = "-",
                    Path = pythonHome,
                    IsInPath = true
                });
            }

            return DeduplicateByPath(results);
        }

        public async Task<List<InstalledEnvironment>> ScanNodeJsAsync()
        {
            var results = new List<InstalledEnvironment>();
            var (nodeSuccess, nodeOutput) = await RunCommandAsync("node", "--version");
            if (nodeSuccess)
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Node.js",
                    Name = "node",
                    Version = nodeOutput.Trim(),
                    Path = await FindExecutablePathAsync("node") ?? "PATH",
                    IsInPath = true
                });
            }

            var (npmSuccess, npmOutput) = await RunCommandAsync("npm", "--version");
            if (npmSuccess)
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Node.js",
                    Name = "npm",
                    Version = npmOutput.Trim(),
                    Path = await FindExecutablePathAsync("npm") ?? "PATH",
                    IsInPath = true
                });
            }

            var nvmHome = Environment.GetEnvironmentVariable("NVM_HOME");
            if (!string.IsNullOrEmpty(nvmHome))
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Node.js",
                    Name = "nvm-windows",
                    Version = "-",
                    Path = nvmHome,
                    IsInPath = true
                });
            }

            return results;
        }

        public List<InstalledEnvironment> ScanPhp()
        {
            var results = new List<InstalledEnvironment>();
            var phpHome = Environment.GetEnvironmentVariable("PHP_HOME");
            if (!string.IsNullOrEmpty(phpHome))
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "PHP",
                    Name = "PHP_HOME",
                    Version = "-",
                    Path = phpHome,
                    IsInPath = true
                });
            }

            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in pathDirs)
            {
                var phpExe = Path.Combine(dir.Trim(), "php.exe");
                if (File.Exists(phpExe))
                {
                    results.Add(new InstalledEnvironment
                    {
                        Type = "PHP",
                        Name = "php",
                        Version = GetFileVersion(phpExe),
                        Path = dir.Trim(),
                        IsInPath = true
                    });
                }
            }

            return DeduplicateByPath(results);
        }

        public async Task<List<InstalledEnvironment>> ScanDotNetAsync()
        {
            var results = new List<InstalledEnvironment>();
            var (success, output) = await RunCommandAsync("dotnet", "--list-sdks");
            if (success && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    results.Add(new InstalledEnvironment
                    {
                        Type = ".NET",
                        Name = "SDK",
                        Version = line.Trim().Split(' ')[0],
                        Path = await FindExecutablePathAsync("dotnet") ?? "PATH",
                        IsInPath = true
                    });
                }
            }

            var (rtSuccess, rtOutput) = await RunCommandAsync("dotnet", "--list-runtimes");
            if (rtSuccess && !string.IsNullOrWhiteSpace(rtOutput))
            {
                foreach (var line in rtOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3))
                {
                    results.Add(new InstalledEnvironment
                    {
                        Type = ".NET",
                        Name = "Runtime",
                        Version = line.Trim(),
                        Path = "-",
                        IsInPath = true
                    });
                }
            }

            return results;
        }

        public async Task<List<InstalledEnvironment>> ScanGitAsync()
        {
            var results = new List<InstalledEnvironment>();
            var (success, output) = await RunCommandAsync("git", "--version");
            if (success)
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Git",
                    Name = "git",
                    Version = output.Trim(),
                    Path = await FindExecutablePathAsync("git") ?? "PATH",
                    IsInPath = true
                });
            }

            return results;
        }

        public async Task<List<InstalledEnvironment>> ScanMavenAsync()
        {
            var results = new List<InstalledEnvironment>();
            var (success, output) = await RunCommandAsync("mvn", "--version");
            if (success)
            {
                var versionLine = output.Split('\n').FirstOrDefault(l => l.Contains("Apache Maven")) ?? output;
                results.Add(new InstalledEnvironment
                {
                    Type = "Maven",
                    Name = "mvn",
                    Version = versionLine.Trim(),
                    Path = await FindExecutablePathAsync("mvn") ?? "PATH",
                    IsInPath = true
                });
            }

            var mavenHome = Environment.GetEnvironmentVariable("MAVEN_HOME") ?? Environment.GetEnvironmentVariable("M2_HOME");
            if (!string.IsNullOrEmpty(mavenHome))
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Maven",
                    Name = "MAVEN_HOME",
                    Version = "-",
                    Path = mavenHome,
                    IsInPath = true
                });
            }

            return DeduplicateByPath(results);
        }

        public async Task<List<InstalledEnvironment>> ScanGoAsync()
        {
            var results = new List<InstalledEnvironment>();

            var (success, output) = await RunCommandAsync("go", "version");
            if (success)
            {
                results.Add(new InstalledEnvironment
                {
                    Type = "Go",
                    Name = "go",
                    Version = output.Trim(),
                    Path = await FindExecutablePathAsync("go") ?? "PATH",
                    IsInPath = true
                });
            }

            var goroot = Environment.GetEnvironmentVariable("GOROOT");
            if (!string.IsNullOrEmpty(goroot) && Directory.Exists(goroot))
            {
                var goExe = Path.Combine(goroot, "bin", "go.exe");
                if (File.Exists(goExe))
                {
                    var version = await GetCommandVersionAsync(goExe, "version");
                    results.Add(new InstalledEnvironment
                    {
                        Type = "Go",
                        Name = "GOROOT",
                        Version = version,
                        Path = goroot,
                        IsInPath = !string.IsNullOrEmpty(goroot)
                    });
                }
            }

            var portableGo = Path.Combine(AppPaths.AppsDir, "go", "bin", "go.exe");
            if (File.Exists(portableGo))
            {
                var version = await GetCommandVersionAsync(portableGo, "version");
                results.Add(new InstalledEnvironment
                {
                    Type = "Go",
                    Name = "绿色版 (DevEnv)",
                    Version = version,
                    Path = Path.GetDirectoryName(portableGo) ?? AppPaths.AppsDir,
                    IsInPath = false
                });
            }

            return DeduplicateByPath(results);
        }

        private void ScanRegistryJava(List<InstalledEnvironment> results)
        {
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\JavaSoft\JDK");
                if (baseKey == null) return;

                foreach (var versionName in baseKey.GetSubKeyNames())
                {
                    using var versionKey = baseKey.OpenSubKey(versionName);
                    var javaHome = versionKey?.GetValue("JavaHome") as string;
                    if (!string.IsNullOrEmpty(javaHome))
                    {
                        results.Add(new InstalledEnvironment
                        {
                            Type = "Java",
                            Name = $"JDK {versionName}",
                            Version = versionName,
                            Path = javaHome,
                            IsInPath = false
                        });
                    }
                }
            }
            catch
            {
                // ignore registry errors
            }
        }

        private static List<InstalledEnvironment> DeduplicateByPath(List<InstalledEnvironment> items)
        {
            return items
                .GroupBy(i => $"{i.Type}|{i.Path}|{i.Name}")
                .Select(g => g.First())
                .ToList();
        }

        private static async Task<string> GetCommandVersionAsync(string exePath, string args)
        {
            var (success, output) = await RunCommandAsync(exePath, args);
            return success ? ExtractVersion(output) : "-";
        }

        private static string ExtractVersion(string output)
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"[\d]+\.[\d]+(\.[\d]+)?(\.[\d]+)?");
            return match.Success ? match.Value : output.Trim().Split('\n')[0];
        }

        private static string GetFileVersion(string filePath)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(filePath).FileVersion ?? "-";
            }
            catch
            {
                return "-";
            }
        }

        private static async Task<string?> FindExecutablePathAsync(string name)
        {
            var (success, output) = await RunCommandAsync("where", name);
            return success ? output.Split('\n')[0].Trim() : null;
        }

        private static async Task<(bool Success, string Output)> RunCommandAsync(string fileName, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var combined = string.IsNullOrWhiteSpace(output) ? error : output;
                return (process.ExitCode == 0 || !string.IsNullOrWhiteSpace(combined), combined.Trim());
            }
            catch
            {
                return (false, string.Empty);
            }
        }
    }
}
