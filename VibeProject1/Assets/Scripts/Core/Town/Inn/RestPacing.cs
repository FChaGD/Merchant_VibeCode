using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 휴식 진행 연출 속도 값(Docs/설계/103번 §4.2). 연출용이라 휴식 서비스(Placeholder)를 실제 시스템으로 바꿔도 남아야 해서
    /// 서비스가 아니라 HubUIWiring 인스펙터에 둔다.
    /// </summary>
    [Serializable]
    public sealed class RestPacingSettings
    {
        [SerializeField] private float baseSecondsPerDay = 3f;
        [SerializeField] private float targetTotalSeconds = 20f;
        [SerializeField] private float minSecondsPerDay = 1f;

        public float BaseSecondsPerDay => baseSecondsPerDay;
        public float TargetTotalSeconds => targetTotalSeconds;
        public float MinSecondsPerDay => minSecondsPerDay;
    }

    /// <summary>
    /// 하루당 진행 시간 = max(최소, min(기준, 목표 ÷ 일수))(기획 94 4-8). 짧은 휴식은 하루 3초, 긴 휴식은 전체가 약 20초 안에 끝나고,
    /// 아주 긴 휴식도 하루가 1초보다 빨리 지나가지 않는다.
    /// </summary>
    public static class RestPacing
    {
        public static float SecondsPerDay(int days, RestPacingSettings settings)
        {
            if (days <= 0) return settings.BaseSecondsPerDay;
            return Mathf.Max(settings.MinSecondsPerDay, Mathf.Min(settings.BaseSecondsPerDay, settings.TargetTotalSeconds / days));
        }
    }
}
