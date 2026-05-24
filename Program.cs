// ============================================================================
// 朝夕·光色 - 程序入口
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.0.0
// 说明: 通过 Mutex 确保单实例运行，支持 --hidden 参数启动时隐藏到托盘。
// ============================================================================

using System.Threading;

namespace SolarSync;

internal static class Program
{
    /// <summary>单实例互斥体名称</summary>
    private static readonly Mutex Mutex = new(
        true, "SolarSync-SingleInstance-Mutex");

    [STAThread]
    private static void Main(string[] args)
    {
        // 尝试获取互斥体，防止多个实例同时运行
        if (!Mutex.WaitOne(TimeSpan.Zero, true))
        {
            MessageBox.Show("程序已在运行中", "朝夕·光色",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(args));
        }
        finally
        {
            Mutex.ReleaseMutex();
            Mutex.Dispose();
        }
    }
}
