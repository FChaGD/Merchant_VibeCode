using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터 이동이 마차·시설을 피하도록 보정하는 계약(Docs/설계/72번 §3.4). 캐릭터는 장애물 목록·묶음을 몰라도
    /// 되고 이 세 조작만 본다(ISP). 장애물이 없는 전투는 NullObstacleNavigator를 받아 캐릭터 코드에 null 분기가 없다.
    /// </summary>
    public interface IObstacleNavigator
    {
        /// <summary>목적지가 장애물 회피 반경 안이면 반경 위 가장 가까운 점으로 옮긴다(기획 71번 §4-14).</summary>
        Vector2 CorrectDestination(Vector2 position, Vector2 destination);

        /// <summary>
        /// position에서 destination으로 갈 이번 틱 이동 방향(단위 벡터, 도착했으면 0). ignored(공격 대상)는 피하지 않는다.
        /// state는 캐릭터별로 들고 있는 좌우 결정 - 장애물을 지날 때까지 같은 쪽을 유지하는 데 쓴다.
        /// </summary>
        Vector2 Steer(Vector2 position, Vector2 destination, IDamageable ignored, ref ObstacleAvoidanceState state);

        /// <summary>장애물 반경 안에 들어간 위치를 반경 위로 빼낸다(안전망, 기획 71번 §4-13).</summary>
        Vector2 ResolvePenetration(Vector2 position);

        /// <summary>이 위치의 이동속도 배율 - 붙은 두 마차·시설 사이를 지나는 중이면 감속(설계 72번 §12), 아니면 1.</summary>
        float GetSpeedMultiplier(Vector2 position);
    }

    /// <summary>캐릭터 한 명의 회피 좌우 결정. 기본값(HasValue=false) = 결정 없음.</summary>
    public struct ObstacleAvoidanceState
    {
        public bool HasValue;
        public int ShapeId;
        // +1 = 왼쪽으로 꺾어 장애물을 오른편에 두고 지나감, -1 = 오른쪽으로 꺾음.
        public int Side;
    }

    /// <summary>장애물이 없는 전투(배틀 테스트, 마차 없는 대열)용 - 기존 직선 이동과 같다.</summary>
    public sealed class NullObstacleNavigator : IObstacleNavigator
    {
        public static readonly NullObstacleNavigator Instance = new();

        private NullObstacleNavigator() { }

        public Vector2 CorrectDestination(Vector2 position, Vector2 destination) => destination;

        public Vector2 Steer(Vector2 position, Vector2 destination, IDamageable ignored, ref ObstacleAvoidanceState state)
        {
            var toDestination = destination - position;
            return toDestination.sqrMagnitude > 0.0001f ? toDestination.normalized : Vector2.zero;
        }

        public Vector2 ResolvePenetration(Vector2 position) => position;

        public float GetSpeedMultiplier(Vector2 position) => 1f;
    }
}
