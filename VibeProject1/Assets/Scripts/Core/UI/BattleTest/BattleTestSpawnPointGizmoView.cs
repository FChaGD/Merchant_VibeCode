using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 적 스폰 포인트(고정 12곳, BattleFieldGeometry.SpawnPointCount) 원과 각 지점 마커를 전장에
    /// 표시한다(요구사항: "테스트 씬에서는 실제 보이게") - Scene뷰 전용 OnDrawGizmos가 아니라 실제
    /// LineRenderer/SpriteRenderer라 Game뷰(Play)에서도 항상 보이고 클릭도 가능하다(마커 클릭 → 예약
    /// 패널 연결은 BattleTestUnitPickerView가 담당). 대열 크기(ColumnCount)가 커지면 스폰 반지름도
    /// 같이 커지므로(IBattleFieldGeometry.ComputeSpawnRadius) 매 프레임 다시 그린다
    /// (BattleTestExtentGizmoView와 동일 패턴).
    /// </summary>
    public class BattleTestSpawnPointGizmoView : MonoBehaviour
    {
        private const int CircleSegments = 64;

        [SerializeField] private LineRenderer circleRenderer;
        [SerializeField] private BattleTestSpawnPointMarkerView[] markers;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private BattleTestFieldLayout FieldLayout => simulationRule.FieldLayout;

        private void Start()
        {
            for (var i = 0; i < markers.Length; i++) markers[i].Initialize(i);
        }

        private void Update()
        {
            var columnCount = FieldLayout.ColumnCount;

            DrawCircle(FieldLayout.ComputeSpawnRadius(columnCount));
            RepositionMarkers(columnCount);
            RefreshMarkerReservations();
        }

        private void DrawCircle(float radius)
        {
            circleRenderer.positionCount = CircleSegments + 1;
            for (var i = 0; i <= CircleSegments; i++)
            {
                var angle = i * (360f / CircleSegments) * Mathf.Deg2Rad;
                circleRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private void RepositionMarkers(int columnCount)
        {
            foreach (var marker in markers)
            {
                var position = FieldLayout.ComputeSpawnPoint(marker.SpawnPointIndex, columnCount);
                marker.transform.position = new Vector3(position.x, position.y, 0f);
            }
        }

        private void RefreshMarkerReservations()
        {
            foreach (var marker in markers)
            {
                marker.SetReservation(simulationRule.SpawnPointReservations.Get(marker.SpawnPointIndex));
            }
        }
    }
}
