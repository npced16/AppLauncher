using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace AppLauncher.Shared.Services
{
    /// <summary>
    /// 프로그램 언인스톨 서비스
    /// </summary>
    public static class UninstallSWService
    {
        private static void Log(string message)
        {
            Console.WriteLine($"[Uninstall] {message}");
        }

        /// <summary>
        /// 프로그램 이름으로 언인스톨 문자열을 찾습니다
        /// </summary>
        /// <param name="programName">찾을 프로그램 이름 (부분 일치)</param>
        /// <returns>UninstallString 또는 null</returns>
        public static string? FindUninstallString(string programName)
        {
            Log($"=== '{programName}' 검색 시작 ===");

            string[] registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var basePath in registryPaths)
            {
                Log($"레지스트리 경로 검색: HKLM\\{basePath}");

                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(basePath);
                    if (key == null)
                    {
                        Log($"  -> 키를 열 수 없음");
                        continue;
                    }

                    var subKeyNames = key.GetSubKeyNames();
                    Log($"  -> 서브키 수: {subKeyNames.Length}");

                    foreach (var subKeyName in subKeyNames)
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();

                        if (displayName != null && displayName.Contains(programName, StringComparison.OrdinalIgnoreCase))
                        {
                            var uninstallString = subKey?.GetValue("UninstallString")?.ToString();
                            var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                            var publisher = subKey?.GetValue("Publisher")?.ToString();

                            Log($"*** 찾음! ***");
                            Log($"  DisplayName: {displayName}");
                            Log($"  UninstallString: {uninstallString}");
                            Log($"  InstallLocation: {installLocation}");
                            Log($"  Publisher: {publisher}");
                            Log($"  RegistryKey: {subKeyName}");

                            return uninstallString;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"  -> 오류: {ex.Message}");
                }
            }

            Log($"'{programName}'을(를) 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// 프로그램 언인스톨 실행
        /// </summary>
        /// <param name="programName">프로그램 이름</param>
        /// <param name="silent">사일런트 모드 (기본: true)</param>
        /// <returns>성공 여부</returns>
        public static bool Uninstall(string programName, bool silent = true)
        {
            Log($"=== '{programName}' 언인스톨 시작 (silent={silent}) ===");

            var uninstallString = FindUninstallString(programName);
            if (string.IsNullOrEmpty(uninstallString))
            {
                Log("언인스톨 정보를 찾을 수 없습니다. 종료.");
                return false;
            }

            try
            {
                // MsiExec 방식인 경우
                if (uninstallString.Contains("MsiExec", StringComparison.OrdinalIgnoreCase))
                {
                    Log("MsiExec 방식 감지");
                    return UninstallMsi(uninstallString, silent);
                }
                else
                {
                    Log("EXE 방식 감지");
                    return UninstallExe(uninstallString, silent);
                }
            }
            catch (Exception ex)
            {
                Log($"언인스톨 실행 오류: {ex.GetType().Name}");
                Log($"  Message: {ex.Message}");
                Log($"  StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// MSI 언인스톨 실행
        /// </summary>
        private static bool UninstallMsi(string uninstallString, bool silent)
        {
            Log($"원본 UninstallString: {uninstallString}");

            // MsiExec.exe /I{GUID} 형식에서 GUID 추출
            // /I를 /X로 변경 (Install -> Uninstall)
            var args = uninstallString
                .Replace("MsiExec.exe", "", StringComparison.OrdinalIgnoreCase)
                .Replace("/I", "/X")
                .Trim();

            Log($"/I -> /X 변환 후: {args}");

            if (silent)
            {
                args += " /quiet /norestart";
                Log($"사일런트 옵션 추가: {args}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };

            Log($"실행: {startInfo.FileName} {startInfo.Arguments}");
            Log("프로세스 시작 중...");

            // var process = Process.Start(startInfo);
            // if (process == null)
            // {
            //     Log("프로세스 시작 실패! (process == null)");
            //     return false;
            // }

            // Log($"프로세스 시작됨. PID: {process.Id}");
            // Log("프로세스 완료 대기 중...");

            // process.WaitForExit();

            // Log($"프로세스 종료. ExitCode: {process.ExitCode}");
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log("프로세스 시작 실패! (process == null)");
                return false;
            }
            Log($"프로세스 시작됨. PID: {process.Id}");
            Log("프로세스 완료 대기 중...");
            // 30분 타임아웃 설정
            if (!process.WaitForExit(30 * 60 * 1000))
            {
                Log("타임아웃: 프로세스가 30분 내에 완료되지 않음");
                process.Kill();
                return false;
            }
            Log($"프로세스 종료. ExitCode: {process.ExitCode}");
            if (process.ExitCode == 0)
            {
                Log("성공!");
                return true;
            }
            else
            {
                Log($"실패. MSI 오류 코드: {process.ExitCode}");
                // 일반적인 MSI 오류 코드 설명
                switch (process.ExitCode)
                {
                    case 1602: Log("  -> 사용자가 취소함"); break;
                    case 1603: Log("  -> 설치 중 치명적 오류"); break;
                    case 1605: Log("  -> 이 작업은 이 컴퓨터에 설치되지 않은 제품에만 적용됩니다"); break;
                    case 1618: Log("  -> 다른 설치가 진행 중"); break;
                    case 1619: Log("  -> 설치 패키지를 열 수 없음"); break;
                }
                return false;
            }
        }

        /// <summary>
        /// EXE 언인스톨러 실행 (NSIS, Inno Setup 등)
        /// </summary>
        private static bool UninstallExe(string uninstallString, bool silent)
        {
            Log($"원본 UninstallString: {uninstallString}");

            // 따옴표 제거
            var exePath = uninstallString.Trim('"');
            Log($"따옴표 제거 후: {exePath}");

            // 파일 존재 확인
            if (!File.Exists(exePath))
            {
                Log($"파일이 존재하지 않음: {exePath}");
                return false;
            }
            Log($"파일 존재 확인됨");

            // 사일런트 옵션 (NSIS: /S, Inno Setup: /VERYSILENT)
            var args = silent ? "/S" : "";
            Log($"사일런트 옵션: '{args}'");

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };

            Log($"실행: \"{startInfo.FileName}\" {startInfo.Arguments}");
            Log("프로세스 시작 중...");

            // var process = Process.Start(startInfo);
            // if (process == null)
            // {
            //     Log("프로세스 시작 실패! (process == null)");
            //     return false;
            // }

            // Log($"프로세스 시작됨. PID: {process.Id}");
            // Log("프로세스 완료 대기 중...");

            // process.WaitForExit();
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log("프로세스 시작 실패! (process == null)");
                return false;
            }

            Log($"프로세스 시작됨. PID: {process.Id}");
            Log("프로세스 완료 대기 중...");

            // 30분 타임아웃 설정
            if (!process.WaitForExit(30 * 60 * 1000))
            {
                Log("타임아웃: 프로세스가 30분 내에 완료되지 않음");
                process.Kill();
                return false;
            }

            Log($"프로세스 종료. ExitCode: {process.ExitCode}");

            if (process.ExitCode == 0)
            {
                Log("성공!");
                return true;
            }
            else
            {
                Log($"실패. 종료 코드: {process.ExitCode}");
                return false;
            }
        }

        /// <summary>
        /// HBOT Operator 언인스톨 (편의 메서드)
        /// </summary>
        public static bool UninstallHbotOperator(bool silent = true)
        {
            Log("=== HBOT Operator 언인스톨 호출 ===");
            return Uninstall("HBOT Operator", silent);
        }
    }
}
