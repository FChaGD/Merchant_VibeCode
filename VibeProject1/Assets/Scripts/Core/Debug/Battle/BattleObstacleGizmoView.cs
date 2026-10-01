#if UNITY_EDITOR
using Game.Core;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 마차·시설 장애물(Docs/설계/72번 §6·§12)을 그린다: 장애물마다 들어가면 안 되는 반경(빨강)과 회피 반경(주황),
    /// 붙은 두 마차·시설 사이 감속 통로(하늘색 - 중심선과 반폭 경계). 다른 전투 기즈모와 같은 방식으로 BattleManager GameObject에
    /// 붙고 BattleGizmoInstaller가 설치/제거한다. 걷어낼 때는 이 파일(+.meta)과 IObstacleFieldDebugSource를 지우고
    /// Remove Battle Gizmos를 한 번 실행한다. 배틀 테스트 씬은 마차·시설이 없어 아무것도 그리지 않는다.
    /// </summary>
    public class BattleObstacleGizmoView : MonoBehaviour
    {
        private static readonly Color RadiusColor = new(1f, 0.25f, 0.25f, 1f);
        private static readonly Color AvoidRadiusColor = new(1f, 0.6f, 0.1f, 1f);
        private static readonly Color PassageColor = new(0.3f, 0.8f, 1f, 1f);

        private IObstacleFieldDebugSource source;

        private void Awake()
        {
            source = GetComponent<IObstacleFieldDebugSource>();
        }

        private void OnDrawGizmos()
        {
            var field = Application.isPlaying ? source?.DebugObstacleField : null;
            if (field == null) return;

            foreach (var shape in field.Obstacles)
            {
                Vector3 center = shape.Center;
                Gizmos.color = RadiusColor;
                Gizmos.DrawWireSphere(center, shape.Radius);
                Gizmos.color = AvoidRadiusColor;
                Gizmos.DrawWireSphere(center, shape.AvoidRadius);
            }

            Gizmos.color = PassageColor;
            foreach (var passage in field.Passages)
            {
                DrawPassage(passage.CoreA, passage.CoreB, passage.Radius);
            }
        }

        // 감속 구역(캡슐)의 중심선과 양옆 경계선. 양 끝의 반원은 장애물 반경 원과 겹치므로 생략한다.
        private static void DrawPassage(Vector2 a, Vector2 b, float halfWidth)
        {
            var axis = b - a;
            if (axis.sqrMagnitude < 0.000001f) return;
            var normal = new Vector2(-axis.y, axis.x).normalized * halfWidth;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(a + normal, b + normal);
            Gizmos.DrawLine(a - normal, b - normal);
        }
    }
}
#endif
