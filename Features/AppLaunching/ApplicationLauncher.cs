using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.AppLaunching
{
    public class ApplicationLauncher
    {
        private ManagedProcess? _managedProcess = null;
        private readonly object _lock = new object();

        public ApplicationLauncher()
        {
        }

        /// <summary>
        /// 관리 중인 프로세스 정보
        /// </summary>
        public class ManagedProcess
        {
            public Process Process { get; set; }
            public string ExecutablePath { get; set; }
            public DateTime StartTime { get; set; }
            public string Name { get; set; }

            public ManagedProcess(Process process, string executablePath, string name)
            {
                Process = process;
                ExecutablePath = executablePath;
                StartTime = DateTime.Now;
                Name = name;
            }

            public bool IsRunning => Process != null && !Process.HasExited;
            public int ProcessId => Process?.Id ?? -1;
        }

        /// <summary>
        /// 백그라운드에서 대상 프로그램 실행 (자동 버전 체크 제거)
        /// </summary>
        public async Task CheckAndLaunchInBackgroundAsync(LauncherConfig config, Action<string> statusCallback)
        {
            try
            {
                statusCallback("프로그램 실행 중...");
                await Task.Delay(500);

                bool launchSuccess = LaunchTargetApplication(config.TargetExecutable, statusCallback);

                if (launchSuccess)
                {
                    statusCallback("프로그램 실행 완료!");
                    await Task.Delay(1000);
                }
                else
                {
                    statusCallback("프로그램 실행 실패");
                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                statusCallback($"오류 발생: {ex.Message}");
                await Task.Delay(3000);
            }
        }

        private bool LaunchTargetApplication(string executable, Action<string> statusCallback)
        {
            try
            {
                if (!File.Exists(executable))
                {
                    statusCallback($"실행 파일을 찾을 수 없습니다: {executable}");
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                };

                // 작업 디렉토리는 실행 파일의 디렉토리로 자동 설정
                string workingDir = Path.GetDirectoryName(executable) ?? "";
                if (!string.IsNullOrWhiteSpace(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    // 프로세스 저장 (하나만 관리)
                    string processName = Path.GetFileNameWithoutExtension(executable);
                    var managedProcess = new ManagedProcess(process, executable, processName);

                    lock (_lock)
                    {
                        _managedProcess = managedProcess;
                    }

                    // 프로세스 종료 이벤트 구독
                    process.EnableRaisingEvents = true;
                    process.Exited += (sender, e) => OnProcessExited();

                    statusCallback($"프로세스 시작: {processName} (PID: {process.Id})");
                }

                return true;
            }
            catch (Exception ex)
            {
                statusCallback($"실행 오류: {ex.Message}");
                return false;
            }
        }

        private void OnProcessExited()
        {
            lock (_lock)
            {
                _managedProcess = null;
            }
        }

        /// <summary>
        /// 현재 실행 중인 프로세스 반환
        /// </summary>
        public ManagedProcess? GetRunningProcess()
        {
            lock (_lock)
            {
                if (_managedProcess != null && !_managedProcess.IsRunning)
                {
                    _managedProcess = null;
                }
                return _managedProcess;
            }
        }

        /// <summary>
        /// 관리 중인 프로세스 종료
        /// </summary>
        public bool KillProcess()
        {
            try
            {
                lock (_lock)
                {
                    if (_managedProcess != null && _managedProcess.IsRunning)
                    {
                        _managedProcess.Process.Kill();
                        _managedProcess = null;
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 프로세스 상태 요약 정보
        /// </summary>
        public string GetProcessStatusSummary()
        {
            lock (_lock)
            {
                if (_managedProcess != null && !_managedProcess.IsRunning)
                {
                    _managedProcess = null;
                }

                if (_managedProcess == null)
                    return "실행 중인 프로세스 없음";

                return GetDetailedProcessInfo(_managedProcess);
            }
        }

        /// <summary>
        /// 프로세스 상세 정보 가져오기
        /// </summary>
        private string GetDetailedProcessInfo(ManagedProcess mp)
        {
            try
            {
                var process = mp.Process;
                var runningTime = DateTime.Now - mp.StartTime;

                // 기본 정보
                var info = $"프로세스: {mp.Name}\n";
                info += $"PID: {mp.ProcessId}\n";
                info += $"실행 시간: {runningTime.Hours}시간 {runningTime.Minutes}분 {runningTime.Seconds}초\n";

                // 응답 상태
                info += $"응답 상태: {(process.Responding ? "정상" : "응답 없음")}\n";

                // 메모리 사용량
                process.Refresh(); // 최신 정보로 갱신
                long memoryMB = process.WorkingSet64 / 1024 / 1024;
                info += $"메모리 사용량: {memoryMB} MB\n";

                // 스레드 수
                info += $"스레드 수: {process.Threads.Count}\n";

                // 우선순위
                try
                {
                    info += $"우선순위: {process.PriorityClass}\n";
                }
                catch { }

                // 윈도우 타이틀 (있는 경우)
                try
                {
                    if (!string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        info += $"윈도우: {process.MainWindowTitle}\n";
                    }
                }
                catch { }

                // 실행 파일 경로
                info += $"경로: {mp.ExecutablePath}";

                return info;
            }
            catch (Exception ex)
            {
                return $"{mp.Name} (PID: {mp.ProcessId}) - 정보 가져오기 실패: {ex.Message}";
            }
        }

        /// <summary>
        /// AppLauncher 종료 시 관리 중인 프로세스도 종료
        /// </summary>
        public void Cleanup()
        {
            lock (_lock)
            {
                if (_managedProcess != null && _managedProcess.IsRunning)
                {
                    try
                    {
                        _managedProcess.Process.Kill();
                    }
                    catch { }
                    _managedProcess = null;
                }
            }
        }
    }
}
