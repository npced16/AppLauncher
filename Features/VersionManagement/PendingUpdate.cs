using System;
using AppLauncher.Features.MqttControl;

namespace AppLauncher.Features.VersionManagement
{
    /// <summary>
    /// 런처 재시작 후 실행할 업데이트 정보
    /// </summary>
    public class PendingUpdate
    {
        /// <summary>
        /// 업데이트 명령
        /// </summary>
        public LaunchCommand Command { get; set; } = null!;

        /// <summary>
        /// 예약된 시간
        /// </summary>
        public DateTime ScheduledTime { get; set; }

        /// <summary>
        /// 업데이트 설명
        /// </summary>
        public string Description { get; set; } = "";
    }
}
