using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 장애물 모양 하나. 마차·시설(원)과 붙은 두 마차·시설 사이 통로(캡슐)를 "기준 도형(점 또는 선분)까지 거리 − 반경" 하나로
    /// 표현한다 - 원은 선분이 점으로 줄어든 경우라 거리 계산이 모양 종류를 가리지 않는다(설계 72번 §3.3·§12).
    /// </summary>
    public readonly struct ObstacleShape
    {
        public readonly int Id;
        // 선분의 두 끝점. 원은 두 점이 같다.
        public readonly Vector2 CoreA;
        public readonly Vector2 CoreB;
        // 들어가면 안 되는 반경(빼내기 기준). 통로는 감속 구역의 반폭.
        public readonly float Radius;
        // 반경 바깥으로 더 띄워 지나갈 여유(조향 기준은 Radius + Clearance). 통로는 0.
        public readonly float Clearance;

        private ObstacleShape(int id, Vector2 coreA, Vector2 coreB, float radius, float clearance)
        {
            Id = id;
            CoreA = coreA;
            CoreB = coreB;
            Radius = radius;
            Clearance = clearance;
        }

        public static ObstacleShape Circle(int id, Vector2 center, float radius, float clearance) => new(id, center, center, radius, clearance);
        public static ObstacleShape Segment(int id, Vector2 a, Vector2 b, float radius) => new(id, a, b, radius, 0f);

        public Vector2 Center => CoreA;
        public float AvoidRadius => Radius + Clearance;

        public Vector2 ClosestCorePoint(Vector2 point) => ClosestPointOnSegment(CoreA, CoreB, point);

        public bool IsWithin(Vector2 point, float radius) => (point - ClosestCorePoint(point)).sqrMagnitude < radius * radius;

        // 이동 선분과 기준 도형 사이 최단 거리 - 둘 다 볼록 집합이라 번갈아 투영하면 수렴한다. 원(점)은 첫 반복에서 정확하다.
        public float DistanceToSegment(Vector2 a, Vector2 b)
        {
            var onCore = ClosestCorePoint(a);
            var onSegment = a;
            for (var i = 0; i < 4; i++)
            {
                onSegment = ClosestPointOnSegment(a, b, onCore);
                onCore = ClosestCorePoint(onSegment);
            }
            return (onSegment - onCore).magnitude;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 0.000001f) return a;
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
            return a + ab * t;
        }
    }

    /// <summary>
    /// 전투 하나의 마차·시설 장애물 정보 + 조향 계산(Docs/기획/71번, 설계 72번). 전투 시작 때 한 번 만든다 - 마차·시설은
    /// 움직이지 않는다. 파괴돼도 전투가 계속되므로(Docs/기획/73번) 조회할 때마다 살아 있는지 보고 파괴된 것은 건너뛴다 -
    /// 목록을 다시 만들면 캐릭터가 기억한 장애물 Id(ObstacleAvoidanceState)가 어긋난다(설계 74번 §2.6).
    /// 마차·시설 하나하나만 피하고, 붙은 둘 사이는 지나갈 수 있되 느려진다(2026-10-01 전제 변경, 설계 72번 §12) - 붙은 마차를
    /// 하나로 묶어 바깥으로 돌게 했더니 ㄷ자 안쪽에 들어간 캐릭터가 빠져나오지 못했고, 막으려면 길 찾기가 필요해 구조가 커졌다.
    /// "붙은" = 정비창 칸 8방향 이웃(마차·시설 구분 없음). 한 칸 이상 떨어진 둘 사이는 보통 길이다.
    /// </summary>
    public sealed class BattleObstacleField : IObstacleNavigator
    {
        private readonly ObstacleShape[] obstacles;
        private readonly IDamageable[] owners;
        // 붙은 두 마차·시설 중심을 잇는 감속 구역(반폭 = 둘 중 큰 장애물 반경). 양 끝이 모두 살아 있을 때만 유효.
        private readonly ObstacleShape[] passages;
        private readonly (int A, int B)[] passageEnds;

        public IReadOnlyList<ObstacleShape> Obstacles => obstacles;
        public IReadOnlyList<ObstacleShape> Passages => passages;

        public bool IsObstacleActive(int index) => owners[index].IsAlive;
        public bool IsPassageActive(int index) => owners[passageEnds[index].A].IsAlive && owners[passageEnds[index].B].IsAlive;

        /// <summary>장애물이 없으면 NullObstacleNavigator를 돌려준다 - 마차 없는 전투에서 계산 비용이 없다.</summary>
        public static IObstacleNavigator Create(IReadOnlyList<ProtectedPlacement> placements) =>
            placements == null || placements.Count == 0 ? NullObstacleNavigator.Instance : new BattleObstacleField(placements);

        public BattleObstacleField(IReadOnlyList<ProtectedPlacement> placements)
        {
            var count = placements.Count;
            obstacles = new ObstacleShape[count];
            owners = new IDamageable[count];
            for (var i = 0; i < count; i++)
            {
                var unit = placements[i].Unit;
                owners[i] = unit;
                obstacles[i] = ObstacleShape.Circle(
                    i, unit.Position,
                    ObstacleAvoidanceTuning.ComputeObstacleRadius(unit.HalfSize),
                    ObstacleAvoidanceTuning.ComputeClearance(unit.HalfSize));
            }
            (passages, passageEnds) = BuildPassages(placements, obstacles);
        }

        public Vector2 CorrectDestination(Vector2 position, Vector2 destination)
        {
            // 장애물끼리 회피 반경이 겹치면 한 번 옮긴 점이 다른 장애물 안일 수 있어 한 번 더 돈다.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < obstacles.Length; i++)
                {
                    var shape = obstacles[i];
                    if (!owners[i].IsAlive || !shape.IsWithin(destination, shape.AvoidRadius)) continue;
                    destination = PushOutTo(shape, destination, shape.AvoidRadius, fallbackFrom: position);
                }
            }
            return destination;
        }

        public Vector2 Steer(Vector2 position, Vector2 destination, IDamageable ignored, ref ObstacleAvoidanceState state)
        {
            var toDestination = destination - position;
            var distance = toDestination.magnitude;
            if (distance < 0.0001f)
            {
                state = default;
                return Vector2.zero;
            }
            var direction = toDestination / distance;

            if (!TryFindBlockingObstacle(position, destination, direction, ignored, out var blocking))
            {
                state = default;
                return direction;
            }

            if (!state.HasValue || state.ShapeId != blocking.Id)
            {
                state = new ObstacleAvoidanceState { HasValue = true, ShapeId = blocking.Id, Side = ChooseSide(blocking, position, direction) };
            }
            return ComputeAvoidDirection(blocking, position, direction, state.Side);
        }

        public Vector2 ResolvePenetration(Vector2 position)
        {
            for (var i = 0; i < obstacles.Length; i++)
            {
                var shape = obstacles[i];
                if (owners[i].IsAlive && shape.IsWithin(position, shape.Radius))
                {
                    position = PushOutTo(shape, position, shape.Radius, fallbackFrom: position + Vector2.right);
                }
            }
            return position;
        }

        public float GetSpeedMultiplier(Vector2 position)
        {
            for (var i = 0; i < passages.Length; i++)
            {
                if (IsPassageActive(i) && passages[i].IsWithin(position, passages[i].Radius)) return ObstacleAvoidanceTuning.PassageSpeedMultiplier;
            }
            return 1f;
        }

        // 이동 선분을 회피 반경 안으로 지나가는 장애물 중 가장 가까운 것. 파괴된 것, 공격 대상, 멀리 있는 것(가장자리까지 감지
        // 거리 초과), 이미 멀어지는 방향인 것은 무시한다.
        private bool TryFindBlockingObstacle(Vector2 position, Vector2 destination, Vector2 direction, IDamageable ignored, out ObstacleShape blocking)
        {
            blocking = default;
            var found = false;
            var nearest = float.MaxValue;
            for (var i = 0; i < obstacles.Length; i++)
            {
                if (owners[i] == ignored || !owners[i].IsAlive) continue;

                var shape = obstacles[i];
                var offset = position - shape.Center;
                var centerDistance = offset.magnitude;
                if (centerDistance - shape.AvoidRadius > ObstacleAvoidanceTuning.LookAheadDistance) continue;
                if (centerDistance > 0.0001f && Vector2.Dot(direction, offset) >= 0f) continue;
                if (shape.DistanceToSegment(position, destination) >= shape.AvoidRadius) continue;
                if (centerDistance >= nearest) continue;

                nearest = centerDistance;
                blocking = shape;
                found = true;
            }
            return found;
        }

        // 두 접선 중 목적지 방향에서 덜 벗어나는 쪽. 거의 같으면(정면) 오른쪽(기획 71번 §4-4·5).
        private static int ChooseSide(ObstacleShape shape, Vector2 position, Vector2 direction)
        {
            var leftAlignment = Vector2.Dot(ComputeAvoidDirection(shape, position, direction, 1), direction);
            var rightAlignment = Vector2.Dot(ComputeAvoidDirection(shape, position, direction, -1), direction);
            if (Mathf.Abs(leftAlignment - rightAlignment) < ObstacleAvoidanceTuning.SideTieThreshold) return -1;
            return leftAlignment > rightAlignment ? 1 : -1;
        }

        // 회피 반경 밖이면 회피 원의 접점 방향, 안이면 원을 따라 미끄러지며 조금씩 바깥으로.
        private static Vector2 ComputeAvoidDirection(ObstacleShape shape, Vector2 position, Vector2 fallbackDirection, int side)
        {
            var offset = position - shape.Center;
            var centerDistance = offset.magnitude;
            var toCenter = centerDistance > 0.0001f ? -offset / centerDistance : fallbackDirection;
            var avoidRadius = shape.AvoidRadius;

            if (centerDistance > avoidRadius)
            {
                var tangentAngle = Mathf.Asin(avoidRadius / centerDistance);
                return Rotate(toCenter, side * tangentAngle);
            }

            var tangent = Rotate(toCenter, side * Mathf.PI * 0.5f);
            var outward = -toCenter * (1f - centerDistance / avoidRadius);
            return (tangent + outward).normalized;
        }

        private static Vector2 PushOutTo(ObstacleShape shape, Vector2 point, float radius, Vector2 fallbackFrom)
        {
            var core = shape.ClosestCorePoint(point);
            var outward = point - core;
            if (outward.sqrMagnitude < 0.000001f) outward = fallbackFrom - core;
            if (outward.sqrMagnitude < 0.000001f) outward = Vector2.right;
            return core + outward.normalized * radius;
        }

        private static Vector2 Rotate(Vector2 vector, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        // 칸 8방향으로 붙은 두 마차·시설마다 중심을 잇는 감속 구역 하나. 대각선으로 붙은 두 마차 사이 빈 칸 중심은 이 선분에서
        // 약 0.71이라 감속 구역(반폭 0.5) 밖이다 - 그 칸에 서 있는 캐릭터는 느려지지 않는다.
        private static (ObstacleShape[] passages, (int A, int B)[] ends) BuildPassages(IReadOnlyList<ProtectedPlacement> placements, ObstacleShape[] obstacles)
        {
            var result = new List<ObstacleShape>();
            var ends = new List<(int A, int B)>();
            for (var i = 0; i < placements.Count; i++)
            {
                for (var j = i + 1; j < placements.Count; j++)
                {
                    if (Mathf.Abs(placements[i].Column - placements[j].Column) > 1 || Mathf.Abs(placements[i].Row - placements[j].Row) > 1) continue;

                    result.Add(ObstacleShape.Segment(
                        obstacles.Length + result.Count, obstacles[i].Center, obstacles[j].Center,
                        Mathf.Max(obstacles[i].Radius, obstacles[j].Radius)));
                    ends.Add((i, j));
                }
            }
            return (result.ToArray(), ends.ToArray());
        }
    }
}
