using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Core
{
    /// <summary>
    /// 플레이어가 보는 도로 하나 = 출발 도시–도착 도시 쌍(설계 89 §2·§5.2, 2026-10-16). 관문을 지나는 구간(도시a–관문a–관문b–도시b)도 키 하나다.
    /// 도로 조각(RoadKey)이 아니라 도시 쌍에 변경을 두는 이유: 같은 관문을 쓰는 다른 도시 쌍에 변경이 새지 않게 하기 위해서다. 방향 무관.
    /// </summary>
    public readonly struct CityPairKey : IEquatable<CityPairKey>
    {
        public int A { get; }
        public int B { get; }

        private CityPairKey(int a, int b)
        {
            A = a;
            B = b;
        }

        public static CityPairKey Of(int x, int y) => x <= y ? new CityPairKey(x, y) : new CityPairKey(y, x);

        public bool Equals(CityPairKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is CityPairKey other && Equals(other);
        public override int GetHashCode() => (A * 397) ^ B;
        public override string ToString() => $"{A}-{B}";
    }

    /// <summary>도시 쌍 난이도 변경 항목 하나(설계 89 §5.2). Amount = 요청 변경량(자르지 않음). ExpiresOnDay = null이면 영구.</summary>
    public readonly struct CityPairDifficultyChange
    {
        public string Id { get; }
        public int Amount { get; }
        public int? ExpiresOnDay { get; }
        public bool IsTimed => ExpiresOnDay.HasValue;

        public CityPairDifficultyChange(string id, int amount, int? expiresOnDay)
        {
            Id = id;
            Amount = amount;
            ExpiresOnDay = expiresOnDay;
        }
    }

    /// <summary>
    /// 도시 쌍 난이도 조회(상행 계획·현재 구간 재계산·디버그). 증감이 아니라 보이는 값을 돌려준다 - 자르기 규칙(영구 먼저)을 읽는 쪽마다 두지 않게.
    /// Changed = 항목이 더해지거나 만료된 쌍.
    /// </summary>
    public interface ICityPairDifficultyReader
    {
        int GetDifficulty(CityPairKey pair, int baseDifficulty);
        IReadOnlyList<CityPairDifficultyChange> GetChanges(CityPairKey pair);
        event Action<CityPairKey> Changed;
    }

    /// <summary>도시 쌍 난이도 변경 - 이벤트 효과와 이벤트 디버그만 쓴다. expiresOnDay = null이면 영구. 발급한 사건 Id를 돌려준다(변경량 0이면 null).</summary>
    public interface ICityPairDifficultyEditor : ICityPairDifficultyReader
    {
        string Apply(CityPairKey pair, string sourceEventId, int change, int? expiresOnDay);
    }

    /// <summary>
    /// 사건 Id 접두(설계 89 §5.2) - `이벤트Id-MMDD`. 월일을 붙여 써서 구분자 `-`가 세 부분 사이에만 온다.
    /// 예약 접두(디버그·이벤트 Id 없음)를 여기 모아 두는 이유: 콘텐츠 검증이 같은 목록으로 이벤트 Id를 막아야 사건 Id가 겹치지 않는다.
    /// </summary>
    public static class CityPairDifficultyIds
    {
        public const string DebugSource = "debug";
        public const string FallbackSource = "event";

        public static bool IsReserved(string eventId) => eventId == DebugSource || eventId == FallbackSource;

        public static string Prefix(string sourceEventId, GameDate date)
            => $"{(string.IsNullOrEmpty(sourceEventId) ? FallbackSource : sourceEventId)}-{date.Month:00}{date.Day:00}";
    }

    /// <summary>
    /// 도시 쌍 변경 항목 저장(설계 89 §5.2, 2026-10-16 보완 검진) - 순수. 변경 하나당 항목 하나(사건 Id, 요청 변경량[, 만료일])로 두고 범위 자르기는
    /// 읽을 때 한다: 보이는 값 = 자르기(자르기(기본 + 영구 합) + 기간 합). 두 칸 누적 + 만료일 하나로 두면 상한에서 효과 없던 기간 변경이 만료일만 덮어써
    /// 다른 기간 변경을 앞당겨 끝냈다. 요청량을 그대로 두므로 항목 하나를 빼면(만료, 향후 Id로 제거) 그 변경만 정확히 빠진다.
    /// 순번은 접두별로 세션 동안 재사용하지 않는다 - 연도가 표시되지 않아 같은 월일이 다시 와도 Id가 겹치지 않게.
    /// </summary>
    public sealed class CityPairDifficultyOverrides
    {
        private static readonly CityPairDifficultyChange[] None = Array.Empty<CityPairDifficultyChange>();

        // 조회는 읽기 전용 보기로 내준다 - 내부 List를 그대로 주면 캐스팅으로 항목을 고칠 수 있다. 보기는 쌍마다 한 번만 만든다(디버그가 매 프레임 조회).
        private sealed class PairChanges
        {
            public readonly List<CityPairDifficultyChange> List = new();
            public readonly ReadOnlyCollection<CityPairDifficultyChange> View;
            public PairChanges() => View = List.AsReadOnly();
        }

        private readonly Dictionary<CityPairKey, PairChanges> entries = new();
        private readonly Dictionary<string, int> lastSequence = new();

        public int TrackedPairCount => entries.Count;

        public int GetDifficulty(CityPairKey pair, int baseDifficulty)
        {
            if (!entries.TryGetValue(pair, out var changes)) return TripTravelRules.ClampDifficulty(baseDifficulty);
            var permanent = 0;
            var timed = 0;
            foreach (var change in changes.List)
            {
                if (change.IsTimed) timed += change.Amount;
                else permanent += change.Amount;
            }
            return TripTravelRules.ClampDifficulty(TripTravelRules.ClampDifficulty(baseDifficulty + permanent) + timed);
        }

        public IReadOnlyList<CityPairDifficultyChange> GetChanges(CityPairKey pair) => entries.TryGetValue(pair, out var changes) ? changes.View : None;

        public string Apply(CityPairKey pair, string sourceEventId, GameDate today, int change, int? expiresOnDay)
        {
            if (change == 0) return null;
            var prefix = CityPairDifficultyIds.Prefix(sourceEventId, today);
            lastSequence.TryGetValue(prefix, out var sequence);
            lastSequence[prefix] = ++sequence;
            var id = $"{prefix}-{sequence}";
            if (!entries.TryGetValue(pair, out var changes)) entries[pair] = changes = new PairChanges();
            changes.List.Add(new CityPairDifficultyChange(id, change, expiresOnDay));
            return id;
        }

        /// <summary>만료일이 today 이하인 기간 항목만 지우고, 항목이 지워진 쌍을 새 목록으로 돌려준다(호출자가 순회 중 다시 Apply해도 안전). 빈 쌍 기록은 지운다.</summary>
        public List<CityPairKey> ExpireThrough(int today)
        {
            var expired = new List<CityPairKey>();
            foreach (var pair in entries)
            {
                if (pair.Value.List.Exists(c => c.ExpiresOnDay is { } day && day <= today)) expired.Add(pair.Key);
            }
            foreach (var key in expired)
            {
                var list = entries[key].List;
                list.RemoveAll(c => c.ExpiresOnDay is { } day && day <= today);
                if (list.Count == 0) entries.Remove(key);
            }
            return expired;
        }
    }
}
