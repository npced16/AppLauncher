using System.Reflection;

namespace AppLauncher.Shared
{
    /// <summary>
    /// 런처의 버전 정보를 관리하는 클래스
    /// </summary>
    public static class VersionInfo
    {
        /// <summary>
        /// 현재 런처의 버전
        /// Properties/AssemblyInfo.cs와 app.manifest의 assemblyIdentity version에서 자동으로 가져옵니다
        /// </summary>
        public static readonly string LAUNCHER_VERSION =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
