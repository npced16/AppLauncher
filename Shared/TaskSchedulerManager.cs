using System;
using System.Diagnostics;

namespace AppLauncher.Shared
{
  public class TaskSchedulerManager
  {
    private const string TASK_NAME = "AppLauncher_Startup";

    /// <summary>
    /// 작업 스케줄러에 등록되어 있는지 확인
    /// </summary>
    public static bool IsTaskRegistered()
    {
      try
      {
        var startInfo = new ProcessStartInfo
        {
          FileName = "schtasks.exe",
          Arguments = $"/Query /TN \"{TASK_NAME}\"",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = true,
          RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
        return process?.ExitCode == 0;
      }
      catch
      {
        return false;
      }
    }

    /// <summary>
    /// 작업 스케줄러에 등록 (로그온 시 자동 실행, 관리자 권한)
    /// Program Files의 정식 설치 경로만 등록
    /// </summary>
    public static bool RegisterTask(string exePath)
    {
      try
      {
        // 항상 Program Files 경로 사용 (파라미터 무시)
        string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string targetExePath = System.IO.Path.Combine(programFilesPath, "AppLauncher", "AppLauncher.exe");

        DebugLogger.Log($"[TaskScheduler] 작업 스케줄러 등록 경로: {targetExePath}");

        // XML 형식으로 작업 생성
        string xmlTask = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>AppLauncher auto-start with administrator privileges</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{targetExePath}</Command>
    </Exec>
  </Actions>
</Task>";

        // 임시 XML 파일 생성
        string tempXmlPath = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempXmlPath, xmlTask, System.Text.Encoding.Unicode);

        try
        {
          var startInfo = new ProcessStartInfo
          {
            FileName = "schtasks.exe",
            Arguments = $"/Create /TN \"{TASK_NAME}\" /XML \"{tempXmlPath}\" /F",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
          };

          using var process = Process.Start(startInfo);
          process?.WaitForExit();

          return process?.ExitCode == 0;
        }
        finally
        {
          // 임시 파일 삭제
          try { System.IO.File.Delete(tempXmlPath); } catch { }
        }
      }
      catch
      {
        return false;
      }
    }
  }
}
