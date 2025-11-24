using System;

namespace AppLauncher.Shared
{
    /// <summary>
    /// 전역 디버그 로거 - Release 빌드에서는 로그가 출력되지 않음
    /// </summary>
    public static class DebugLogger
    {
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        public static void Log(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }

        /// <summary>
        /// 태그와 함께 디버그 로그 출력
        /// </summary>
        public static void Log(string tag, string message)
        {
#if DEBUG
            Console.WriteLine($"[{tag}] {message}");
#endif
        }
    }
}
