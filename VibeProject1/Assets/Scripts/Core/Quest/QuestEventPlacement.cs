using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>확정 사건이 일어날 자리 - 몇 번째 구간의 구간 진행률 몇에서.</summary>
    public readonly struct QuestEventSpot
    {
        public int LegIndex { get; }
        public float LegProgress { get; }

        public QuestEventSpot(int legIndex, float legProgress)
        {
            LegIndex = legIndex;
            LegProgress = legProgress;
        }
    }

    /// <summary>
    /// 퀘스트 확정 사건의 발생 위치(Docs/설계/107번 §9.3, 2026-10-27 사용자 결정 - 진행률 기반·무작위 구간 둘 다). 상행 시작 때 한 번 계산해 고정한다 -
    /// 구간 실행 시간이 난이도 변경·궤주로 달라질 때 다시 계산하면 이미 지난 자리로 옮겨 발생이 빠질 수 있다.
    /// 이벤트 조건 판정은 구간 진행률만 알기 때문에 전체 진행률을 (구간, 구간 진행률)로 바꿔 둔다. 구간 경계는 다음 구간 0(그 구간에 들어서는 순간).
    /// 새 방식은 열거값 하나와 분기 하나로 늘린다.
    /// </summary>
    public static class QuestEventPlacement
    {
        public static QuestEventSpot Resolve(QuestEventTiming timing, IReadOnlyList<float> legDurations, float min, float max, Func<float> random01)
        {
            var legCount = legDurations?.Count ?? 0;
            switch (timing)
            {
                case QuestEventTiming.Random:
                {
                    if (min > max) (min, max) = (max, min);
                    min = Mathf.Clamp01(min);
                    max = Mathf.Clamp01(max);
                    var leg = legCount == 0 ? 0 : Mathf.Min(Mathf.FloorToInt(random01() * legCount), legCount - 1);
                    return new QuestEventSpot(Mathf.Max(0, leg), min + (max - min) * random01());
                }
                default:
                    return FromOverallProgress(legDurations, Mathf.Clamp01(min));
            }
        }

        private static QuestEventSpot FromOverallProgress(IReadOnlyList<float> legDurations, float progress)
        {
            var legCount = legDurations?.Count ?? 0;
            var total = 0f;
            for (var i = 0; i < legCount; i++) total += Mathf.Max(0f, legDurations[i]);
            if (legCount == 0 || total <= 0f) return new QuestEventSpot(0, progress);
            if (progress >= 1f) return new QuestEventSpot(legCount - 1, 1f);

            var target = progress * total;
            var start = 0f;
            for (var i = 0; i < legCount; i++)
            {
                var duration = Mathf.Max(0f, legDurations[i]);
                var end = start + duration;
                if (target < end) return new QuestEventSpot(i, duration > 0f ? (target - start) / duration : 0f);
                start = end;
            }
            return new QuestEventSpot(legCount - 1, 1f);
        }
    }
}
