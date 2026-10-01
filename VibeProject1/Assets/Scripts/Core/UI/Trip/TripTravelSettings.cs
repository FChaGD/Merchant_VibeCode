using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>거리 구분 하나(Docs/기획/75번 §4.1). 시간은 초, 거리는 지역 지도 좌표.</summary>
    [Serializable]
    public struct TripDistanceBand
    {
        public string Name;
        public float MinSeconds;
        public float SpanSeconds;
        public float StartDistance;
        public float DistanceSpan;

        public TripDistanceBand(string name, float minSeconds, float spanSeconds, float startDistance, float distanceSpan)
        {
            Name = name;
            MinSeconds = minSeconds;
            SpanSeconds = spanSeconds;
            StartDistance = startDistance;
            DistanceSpan = distanceSpan;
        }
    }

    /// <summary>난이도 등급 하나(Docs/기획/75번 §4.2). 상한 이하 난이도가 이 등급이다.</summary>
    [Serializable]
    public struct TripDifficultyGrade
    {
        public string Name;
        public int MaxDifficulty;
        public float EncountersPerFiveMinutes;

        public TripDifficultyGrade(string name, int maxDifficulty, float encountersPerFiveMinutes)
        {
            Name = name;
            MaxDifficulty = maxDifficulty;
            EncountersPerFiveMinutes = encountersPerFiveMinutes;
        }
    }

    /// <summary>
    /// 상행 이동 수치(Docs/설계/76번 §3.2) - 소요시간 공식의 n·거리 구분, 난이도 등급별 인카운터 빈도. 밸런스 수치라 테이블이 아니라
    /// 인스펙터로 둔다(마을 규모 시설 배정과 같은 방식). 기본값은 Default이고, 인스톨러가 비어 있을 때만 채운다.
    /// 거리 구분은 시작 거리 오름차순, 마지막 구분이 "긴 거리"(지역을 넘는 구간이 항상 쓰는 구분)다.
    /// </summary>
    [Serializable]
    public class TripTravelSettings
    {
        public const float LegacyDurationSeconds = 30f;
        public const int DefaultRoadDifficulty = 50;

        [Tooltip("난이도가 늘릴 수 있는 최대 시간(초) - 모든 거리 구분 공통. 가장 작은 시간 폭을 넘으면 안 된다.")]
        public float DifficultyTimeSeconds = 36f;
        public List<TripDistanceBand> DistanceBands = new();
        public List<TripDifficultyGrade> DifficultyGrades = new();

        public static TripTravelSettings Default() => new()
        {
            DifficultyTimeSeconds = 36f,
            DistanceBands = new List<TripDistanceBand>
            {
                new("짧음", 60f, 120f, 0f, 600f),
                new("중간", 300f, 180f, 600f, 500f),
                new("긴", 900f, 300f, 1100f, 1100f),
            },
            DifficultyGrades = new List<TripDifficultyGrade>
            {
                new("매우 쉬움", 14, 1.25f),
                new("쉬움", 28, 1.5f),
                new("조금 쉬움", 42, 1.75f),
                new("보통", 57, 2.0f),
                new("약간 어려움", 71, 2.25f),
                new("어려움", 85, 2.5f),
                new("매우 어려움", 100, 2.75f),
            },
        };

        public bool IsValid => DistanceBands is { Count: > 0 } && DifficultyGrades is { Count: > 0 };
    }
}
