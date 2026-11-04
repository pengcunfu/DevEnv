using System;
using System.IO;
using System.Text;

namespace DevEnv.Host.SigXor.Diagnostics;

/// <summary>
/// 探针输出缓冲。
///
/// 探针跑在 WinExe 进程里，没有控制台可写（Console.WriteLine 的输出会丢），
/// 因此所有探针把结果写进这里的共享缓冲区，最后由调用方一次性落盘。
/// </summary>
public static class ProbeOutput
{
    private static readonly StringBuilder Buffer = new();

    public static void Line(string text) => Buffer.AppendLine(text);

    public static void Blank() => Buffer.AppendLine();

    public static void Section(string title)
    {
        Buffer.AppendLine();
        Buffer.AppendLine(title);
    }

    public static string Flush() => Buffer.ToString();
}
