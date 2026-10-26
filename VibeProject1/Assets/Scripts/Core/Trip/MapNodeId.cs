using System;

namespace Game.Core
{
    public enum MapNodeKind
    {
        City,
        Gate,
    }

    /// <summary>
    /// 지도 도로의 끝점 - 도시 또는 관문(지역 아이콘)(Docs/설계/69번 §4.1). 도시 Id와 관문 Id는 번호 공간이 따로라
    /// 정수 하나로는 끝점을 가릴 수 없어 종류를 함께 둔다. 엑셀 표기는 `C`+도시 Id / `G`+관문 Id(예: `C4`, `G1`, 설계 69번 §3.1 안 A).
    /// 자산에 직렬화되므로 필드는 공개 가변이다(Unity 직렬화 제약) - 값 비교는 Kind·Id로만 한다.
    /// </summary>
    [Serializable]
    public struct MapNodeId : IEquatable<MapNodeId>
    {
        private const char CityPrefix = 'C';
        private const char GatePrefix = 'G';

        public MapNodeKind Kind;
        public int Id;

        public MapNodeId(MapNodeKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static MapNodeId City(int id) => new(MapNodeKind.City, id);
        public static MapNodeId Gate(int id) => new(MapNodeKind.Gate, id);

        public bool IsCity => Kind == MapNodeKind.City;

        public static bool TryParse(string text, out MapNodeId node)
        {
            node = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var trimmed = text.Trim();
            var prefix = char.ToUpperInvariant(trimmed[0]);
            if (prefix != CityPrefix && prefix != GatePrefix) return false;
            if (!int.TryParse(trimmed.Substring(1), out var id)) return false;

            node = new MapNodeId(prefix == CityPrefix ? MapNodeKind.City : MapNodeKind.Gate, id);
            return true;
        }

        public override string ToString() => $"{(Kind == MapNodeKind.City ? CityPrefix : GatePrefix)}{Id}";

        public bool Equals(MapNodeId other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is MapNodeId other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id;
        public static bool operator ==(MapNodeId a, MapNodeId b) => a.Equals(b);
        public static bool operator !=(MapNodeId a, MapNodeId b) => !a.Equals(b);
    }
}
