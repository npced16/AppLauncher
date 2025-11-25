using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.AppLaunching
{
    public class ApplicationLauncher
    {
        private static void Log(string message) => DebugLogger.Log("AppLauncher", message);

        private ManagedProcess? _managedProcess = null;
        private readonly object _lock = new object();
        private IntPtr _jobHandle = IntPtr.Zero;

        // Windows Job Object API
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? name);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr job, JobObjectInfoType infoType,
            IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        public ApplicationLauncher()
        {
            // Job Object 생성 (부모가 종료되면 자식도 종료)
            _jobHandle = CreateJobObject(IntPtr.Zero, null);
            if (_jobHandle == IntPtr.Zero)
            {
                Log("Job Object 생성 실패");
                return;
            }

            // Job에 "부모 종료 시 자식도 종료" 플래그 설정
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(info, extendedInfoPtr, false);

            bool result = SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation,
                extendedInfoPtr, (uint)length);

            Marshal.FreeHGlobal(extendedInfoPtr);

            if (result)
            {
                Log("Job Object 설정 완료 (부모 종료 시 자식도 자동 종료)");
            }
            else
            {
                Log("Job Object 설정 실패");
            }
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
        /// 백그라운드에서 대상 프로그램 실행 
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
                    UseShellExecute = false, // Job Object 할당을 위해 필수
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
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
                    // Job Object에 프로세스 할당 (부모-자식 관계 설정)
                    if (_jobHandle != IntPtr.Zero)
                    {
                        bool assigned = AssignProcessToJobObject(_jobHandle, process.Handle);
                        if (assigned)
                        {
                            Log($"프로세스를 Job Object에 할당 완료 (PID: {process.Id})");
                            Log("AppLauncher 종료 시 자동으로 함께 종료됩니다");
                        }
                        else
                        {
                            Log($"Job Object 할당 실패 (PID: {process.Id})");
                        }
                    }

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

                return process != null;
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
                // Job Object를 닫으면 자동으로 자식 프로세스도 종료됨
                if (_jobHandle != IntPtr.Zero)
                {
                    Log("Job Object 종료 (모든 자식 프로세스 자동 종료)");
                    CloseHandle(_jobHandle);
                    _jobHandle = IntPtr.Zero;
                }

                if (_managedProcess != null && _managedProcess.IsRunning)
                {
                    try
                    {
                        Log($"프로세스 종료 시도: {_managedProcess.Name} (PID: {_managedProcess.ProcessId})");
                        _managedProcess.Process.Kill();
                        _managedProcess.Process.WaitForExit(3000); // 3초 대기
                        Log($"프로세스 종료 완료");
                    }
                    catch (Exception ex)
                    {
                        Log($"프로세스 종료 실패: {ex.Message}");
                    }
                    _managedProcess = null;
                }
                else
                {
                    Log("_managedProcess가 null이거나 실행 중이지 않음");
                }
            }
        }

        /// <summary>
        /// 프로세스 이름으로 모든 실행 중인 프로세스 종료
        /// </summary>
        public void CleanupByProcessName(string executablePath)
        {
            try
            {
                if (string.IsNullOrEmpty(executablePath))
                    return;

                string processName = Path.GetFileNameWithoutExtension(executablePath);
                DebugLogger.Log($"[ApplicationLauncher] 프로세스 이름으로 종료 시도: {processName}");

                var processes = Process.GetProcessesByName(processName);
                DebugLogger.Log($"[ApplicationLauncher] 발견된 프로세스 수: {processes.Length}");

                foreach (var process in processes)
                {
                    try
                    {
                        DebugLogger.Log($"[ApplicationLauncher] 프로세스 종료: {process.ProcessName} (PID: {process.Id})");
                        process.Kill();
                        process.WaitForExit(3000);
                        DebugLogger.Log($"[ApplicationLauncher] 프로세스 종료 완료: PID {process.Id}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[ApplicationLauncher] 프로세스 종료 실패 (PID {process.Id}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ApplicationLauncher] CleanupByProcessName 오류: {ex.Message}");
            }
        }
    }
}
