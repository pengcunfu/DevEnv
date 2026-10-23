using System.Diagnostics;
using System.IO;
using MenuManager.Models;
using Newtonsoft.Json;

namespace MenuManager.Services
{
    /// <summary>
    /// 需要管理员权限的注册表操作类型
    /// </summary>
    public enum ElevatedRegistryOperation
    {
        AddMenu,
        RemoveMenu
    }

    /// <summary>
    /// 提权操作请求（通过临时文件传递给提权进程）
    /// </summary>
    public class ElevatedOperationRequest
    {
        public ElevatedRegistryOperation Operation { get; set; }

        public MenuConfig Config { get; set; } = new MenuConfig();

        public string ResultFile { get; set; } = string.Empty;
    }

    /// <summary>
    /// 提权操作结果
    /// </summary>
    public class ElevatedOperationResult
    {
        public bool Success { get; set; }

        public string? Error { get; set; }
    }

    /// <summary>
    /// 管理员权限操作辅助类：
    /// 普通权限下通过 runas 启动自身提权副本执行注册表写入，并返回执行结果。
    /// </summary>
    public static class ElevatedRegistryService
    {
        private const string CommandLineSwitch = "--elevated-op";

        /// <summary>
        /// 在普通权限下请求提权执行注册表操作（会弹出 UAC 确认框）
        /// </summary>
        public static void RunElevated(ElevatedRegistryOperation operation, MenuConfig config)
        {
            var requestFile = Path.Combine(Path.GetTempPath(), $"menumanager_op_{Guid.NewGuid():N}.json");
            var resultFile = Path.Combine(Path.GetTempPath(), $"menumanager_result_{Guid.NewGuid():N}.json");
            string? error = null;

            try
            {
                File.WriteAllText(requestFile, JsonConvert.SerializeObject(new ElevatedOperationRequest
                {
                    Operation = operation,
                    Config = config,
                    ResultFile = resultFile
                }));

                var executable = Environment.ProcessPath;
                if (string.IsNullOrEmpty(executable))
                    throw new InvalidOperationException("无法确定当前程序路径");

                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"\"{CommandLineSwitch}\" \"{requestFile}\"",
                    Verb = "runas",
                    UseShellExecute = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();

                if (!File.Exists(resultFile))
                    throw new InvalidOperationException("提权操作未完成，可能已被用户取消");

                var result = JsonConvert.DeserializeObject<ElevatedOperationResult>(File.ReadAllText(resultFile));
                if (result is not { Success: true })
                    throw new InvalidOperationException(result?.Error ?? "提权操作失败");
            }
            catch (Exception ex)
            {
                error = $"请求管理员权限失败: {ex.Message}";
            }
            finally
            {
                TryDeleteFile(requestFile);
                TryDeleteFile(resultFile);
            }

            if (error != null)
                throw new InvalidOperationException(error);
        }

        /// <summary>
        /// 提权进程入口：从命令行读取请求并执行，返回进程退出码
        /// </summary>
        public static int ExecuteFromCommandLine(string requestFile)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<ElevatedOperationRequest>(File.ReadAllText(requestFile))
                    ?? throw new InvalidOperationException("提权请求数据无效");

                var registry = new RegistryManager();
                switch (request.Operation)
                {
                    case ElevatedRegistryOperation.AddMenu:
                        registry.AddMenu(request.Config);
                        break;
                    case ElevatedRegistryOperation.RemoveMenu:
                        registry.RemoveMenu(request.Config);
                        break;
                    default:
                        throw new InvalidOperationException($"不支持的操作: {request.Operation}");
                }

                WriteResult(request.ResultFile, new ElevatedOperationResult { Success = true });
                return 0;
            }
            catch (Exception ex)
            {
                // 尝试把错误写回，避免主进程拿到空结果
                try
                {
                    var request = JsonConvert.DeserializeObject<ElevatedOperationRequest>(File.ReadAllText(requestFile));
                    if (request != null)
                    {
                        WriteResult(request.ResultFile, new ElevatedOperationResult
                        {
                            Success = false,
                            Error = ex.Message
                        });
                    }
                }
                catch
                {
                    // 忽略，主进程会自行报告失败
                }

                return 1;
            }
        }

        private static void WriteResult(string resultFile, ElevatedOperationResult result)
        {
            if (string.IsNullOrEmpty(resultFile))
                return;

            File.WriteAllText(resultFile, JsonConvert.SerializeObject(result));
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 忽略临时文件清理失败
            }
        }
    }
}
