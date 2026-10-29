using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 알려진 조건 키의 단일 소스(설계 87 §5.1). 임포터(모르는 키 = 오류)와 실행 쪽 판정기 등록부가 같은 목록을 써야 둘이 어긋나지 않는다.
    /// 새 조건 종류(퀘스트·스토리, 기획 86 §5.3)는 여기 키를 더하고 판정기를 등록한다.
    /// </summary>
    public static class EncounterConditionKeys
    {
        public const string Month = "month";
        public const string Date = "date";
        public const string Progress = "progress";
        public const string Region = "region";
        public const string Road = "road";
        public const string From = "from";
        public const string To = "to";
        public const string Flag = "flag";
        public const string Seen = "seen";
        /// <summary>`quest:퀘스트Id` - 그 퀘스트의 확정 사건 위치에 도달했고 아직 발생하지 않았다(설계 107번 §9.3).</summary>
        public const string Quest = "quest";
        /// <summary>`town:도시Id` - 숙소 휴식 중 체류 도시(설계 109 §3.2). 상행 중엔 거짓.</summary>
        public const string Town = "town";

        private static readonly HashSet<string> Known = new() { Month, Date, Progress, Region, Road, From, To, Flag, Seen, Quest, Town };

        // 장소에 없는 값을 보는 조건은 그 장소에서 항상 거짓이라 작성 실수다 - 임포트·검증에서 막는다(설계 109 §3.4).
        private static readonly HashSet<string> TripOnly = new() { From, To, Road, Progress, Quest };
        private static readonly HashSet<string> RestOnly = new() { Town };

        public static bool IsAllowedAt(string key, EventPlace place)
            => place == EventPlace.Rest ? !TripOnly.Contains(key) : !RestOnly.Contains(key);

        public static IReadOnlyCollection<string> All => Known;

        public static bool IsKnown(string key) => key != null && Known.Contains(key);
    }
}
