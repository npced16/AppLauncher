using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AppLauncher
{
    public class VersionChecker
    {
        private readonly string _versionUrl;
        private readonly string _localVersionFile;

        public VersionChecker(string versionUrl, string localVersionFile)
        {
            _versionUrl = versionUrl;
            _localVersionFile = localVersionFile;
        }

        public async Task<VersionCheckResult> CheckVersionAsync()
        {
            try
            {
                // 로컬 버전 읽기
                var localVersion = GetLocalVersion();

                // 원격 버전 확인
                var remoteVersion = await GetRemoteVersionAsync();

                if (remoteVersion == null)
                {
                    return new VersionCheckResult
                    {
                        IsUpdateRequired = false,
                        LocalVersion = localVersion,
                        RemoteVersion = localVersion,
                        Message = "원격 버전 정보를 가져올 수 없습니다. 로컬 버전으로 실행합니다."
                    };
                }

                // 버전 비교
                bool isUpdateRequired = CompareVersions(localVersion, remoteVersion) < 0;

                return new VersionCheckResult
                {
                    IsUpdateRequired = isUpdateRequired,
                    LocalVersion = localVersion,
                    RemoteVersion = remoteVersion,
                    Message = isUpdateRequired
                        ? $"새 버전({remoteVersion})이 있습니다. 업데이트를 진행합니다."
                        : "최신 버전입니다."
                };
            }
            catch (Exception ex)
            {
                return new VersionCheckResult
                {
                    IsUpdateRequired = false,
                    LocalVersion = "0.0.0",
                    RemoteVersion = "0.0.0",
                    Message = $"버전 확인 중 오류: {ex.Message}"
                };
            }
        }

        private string GetLocalVersion()
        {
            try
            {
                if (File.Exists(_localVersionFile))
                {
                    return File.ReadAllText(_localVersionFile).Trim();
                }
            }
            catch { }

            return "0.0.0";
        }

        private async Task<string?> GetRemoteVersionAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetStringAsync(_versionUrl);

                // JSON 형식인 경우
                if (_versionUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = JObject.Parse(response);
                    return json["version"]?.ToString();
                }

                // 텍스트 형식인 경우
                return response.Trim();
            }
            catch
            {
                return null;
            }
        }

        private int CompareVersions(string version1, string version2)
        {
            try
            {
                var v1 = new Version(version1);
                var v2 = new Version(version2);
                return v1.CompareTo(v2);
            }
            catch
            {
                return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public class VersionCheckResult
    {
        public bool IsUpdateRequired { get; set; }
        public string LocalVersion { get; set; } = "0.0.0";
        public string RemoteVersion { get; set; } = "0.0.0";
        public string Message { get; set; } = "";
    }
}
