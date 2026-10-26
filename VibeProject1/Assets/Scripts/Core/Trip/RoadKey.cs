using System;

namespace Game.Core
{
    /// <summary>
    /// 도로 하나를 가리키는 키 - 두 끝점을 종류·Id 순으로 정렬해 방향과 무관하게 같은 도로가 같은 키가 된다(Docs/설계/76번 §3.1).
    /// 도로 난이도(기본값·바뀐 값)를 도로별로 찾을 때 쓴다.
    /// </summary>
    public readonly struct RoadKey : IEquatable<RoadKey>
    {
        public readonly MapNodeId A;
        public readonly MapNodeId B;

        private RoadKey(MapNodeId a, MapNodeId b)
        {
            A = a;
            B = b;
        }

        public static RoadKey Of(MapNodeId a, MapNodeId b)
        {
            var aFirst = a.Kind < b.Kind || (a.Kind == b.Kind && a.Id <= b.Id);
            return aFirst ? new RoadKey(a, b) : new RoadKey(b, a);
        }

        public bool Equals(RoadKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is RoadKey other && Equals(other);
        public override int GetHashCode() => (A.GetHashCode() * 397) ^ B.GetHashCode();
        public override string ToString() => $"{A}-{B}";
    }
}
