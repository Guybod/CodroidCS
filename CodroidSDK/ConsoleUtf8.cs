using System;
using System.Runtime.InteropServices;

namespace Codroid
{
    /// <summary>
    /// 控制台 UTF-8 输出（Windows 下避免中文乱码；Linux/macOS 通常无需处理）。
    /// </summary>
    public static class ConsoleUtf8
    {
        /// <summary>
        /// 将控制台输入/输出编码设为 UTF-8。建议在示例程序 <c>Main</c> 入口首行调用。
        /// </summary>
        public static void InitConsoleUtf8()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                Console.InputEncoding = System.Text.Encoding.UTF8;
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
                // 无控制台或宿主限制时忽略
            }
        }
    }
}
