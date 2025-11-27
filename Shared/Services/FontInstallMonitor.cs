using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using AppLauncher.Shared;

public static class FontInstallMonitor
{

  private static void Log(string message) => DebugLogger.Log("FontInstallMonitor", message);

  /// <summary>
  /// 특정 부모 프로세스의 자식 프로세스 찾기 (WMI 사용)
  /// </summary>
  private static int[] GetChildProcessIds(int parentProcessId)
  {
    try
    {
      var childPids = new System.Collections.Generic.List<int>();
      using (var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentProcessId}"))
      {
        foreach (ManagementObject obj in searcher.Get())
        {
          int childPid = Convert.ToInt32(obj["ProcessId"]);
          childPids.Add(childPid);
        }
      }
      return childPids.ToArray();
    }
    catch (Exception ex)
    {
      Log($"[CHILD_PROCESS] Failed to get child processes for PID {parentProcessId}: {ex.Message}");
      return new int[0];
    }
  }

  /// <summary>
  /// 특정 프로세스의 모든 자손 프로세스 찾기 (재귀적으로)
  /// </summary>
  private static int[] GetAllDescendantProcessIds(int parentProcessId)
  {
    var descendants = new System.Collections.Generic.List<int>();
    var directChildren = GetChildProcessIds(parentProcessId);

    foreach (int childPid in directChildren)
    {
      descendants.Add(childPid);
      descendants.AddRange(GetAllDescendantProcessIds(childPid));
    }

    return descendants.ToArray();
  }

  /// <summary>
  /// fonts_install.exe 프로세스 강제 종료
  /// </summary>
  /// <param name="parentProcessId">부모 프로세스 ID (지정하면 해당 프로세스의 자식만 종료)</param>
  public static void KillFontsInstallProcess(int? parentProcessId = null)
  {
    try
    {
      // "fonts_install" 이름의 모든 프로세스 찾기
      var processes = Process.GetProcessesByName("fonts_install");

      if (processes.Length == 0)
      {
        // 프로세스가 없으면 아무것도 하지 않음 (정상)
        return;
      }

      // 부모 프로세스가 지정된 경우, 해당 부모의 자손 프로세스만 필터링
      int[]? targetPids = null;
      if (parentProcessId.HasValue)
      {
        targetPids = GetAllDescendantProcessIds(parentProcessId.Value);
        if (targetPids.Length > 0)
        {
          Log($"[KILL_PROCESS] Found {targetPids.Length} descendant process(es) of PID {parentProcessId.Value}: {string.Join(", ", targetPids)}");
        }
      }

      foreach (var process in processes)
      {
        try
        {
          // 프로세스가 이미 종료되었는지 확인
          if (process.HasExited)
          {
            Log($"[KILL_PROCESS] Process already exited (PID: {process.Id})");
            continue;
          }

          // 부모 프로세스가 지정된 경우, 해당 부모의 자손인지 확인
          if (parentProcessId.HasValue && targetPids != null && !targetPids.Contains(process.Id))
          {
            Log($"[KILL_PROCESS] Skipping fonts_install (PID: {process.Id}) - not a descendant of PID {parentProcessId.Value}");
            continue;
          }

          Log($"[KILL_PROCESS] Killing fonts_install process (PID: {process.Id})");
          process.Kill();

          // 프로세스가 종료될 때까지 대기 (최대 3초)
          bool exited = process.WaitForExit(3000);

          if (exited)
          {
            Log($"[KILL_PROCESS] Process killed successfully (PID: {process.Id})");
          }
          else
          {
            Log($"[KILL_PROCESS] Process did not exit within timeout (PID: {process.Id})");
          }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
          // 프로세스 접근 권한 없음 또는 프로세스를 찾을 수 없음
          Log($"[KILL_PROCESS] Access denied or process not found (PID: {process.Id}): {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
          // 프로세스가 이미 종료됨
          Log($"[KILL_PROCESS] Process already terminated (PID: {process.Id}): {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
          // 원격 프로세스이거나 지원되지 않음
          Log($"[KILL_PROCESS] Operation not supported (PID: {process.Id}): {ex.Message}");
        }
        catch (Exception ex)
        {
          // 기타 예외
          Log($"[KILL_PROCESS] Unexpected error killing process (PID: {process.Id}): {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
          try
          {
            process.Dispose();
          }
          catch
          {
            // Dispose 실패 무시
          }
        }
      }

      Log($"[KILL_PROCESS] Processed {processes.Length} fonts_install process(es)");
    }
    catch (Exception ex)
    {
      // GetProcessesByName이 실패하는 경우 (매우 드묾)
      Log($"[KILL_PROCESS] Failed to enumerate processes: {ex.GetType().Name} - {ex.Message}");
    }
  }

  /// <summary>
  /// fonts_install.exe 모니터링 후 일정 시간 CPU 변화 없으면 종료
  /// </summary>
  public static void TerminateOnIdle()
  {
    float lastCpu = 0;
    int idleCount = 0;
    int idleThreshold = 50; // 0.1초 * 50 = 5초

    Console.WriteLine("Monitoring   .exe...");

    while (true)
    {
      Process[] processes = Process.GetProcessesByName("fonts_install");

      if (processes.Length == 0)
      {
        Console.WriteLine("[NOT RUNNING] Exiting monitor.");
        break;
      }

      var process = processes[0];
      float currentCpu = (float)process.TotalProcessorTime.TotalMilliseconds;

      if (currentCpu == lastCpu)
        idleCount++;
      else
        idleCount = 0;

      if (idleCount >= idleThreshold)
      {
        Console.WriteLine("[IDLE / TERMINATING PROCESS TREE]");
        KillFontsInstallProcess(process.Id);
        break;
      }

      lastCpu = currentCpu;
      Thread.Sleep(100);
    }

    Console.WriteLine("Monitoring ended.");
  }
}
