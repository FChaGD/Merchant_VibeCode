using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 세팅 단계 유닛/스폰 포인트 마커 클릭 감지. 이 씬은 전장 드래그팬 캡처용 전체화면 배경
    /// (CameraDragCatcher)이 항상 화면을 덮고 있어, uGUI EventSystem에 Physics2DRaycaster를 더해
    /// 콜라이더를 별도 레이캐스터로 잡으면 화면 전체를 덮는 배경 쪽 레이캐스트 결과가 항상 더 앞에
    /// 있는 것으로 처리돼(Screen Space Overlay는 화면에서 가장 가까운 것으로 취급됨) 클릭이 막힌다.
    /// 대신 이 컴포넌트를 CameraDragCatcher 자신에게 붙여, 그 배경이 받은 클릭 좌표를 월드 좌표로
    /// 바꾼 뒤 Physics2D.OverlapPoint로 직접 대상을 찾는다 - 레이캐스터 경합 자체가 생기지 않는다.
    /// OnMouseDown(레거시 Input Manager 기반)을 쓰지 않는 이유는 BattleTestUnitClickTarget 참고.
    /// 유닛과 스폰 포인트 마커는 서로 다른 컴포넌트라 한 클릭에 대해 둘 다 시도해 먼저 맞는 쪽만
    /// 콜백한다(같은 자리에 우연히 겹치는 경우는 드물고, 겹쳐도 큰 문제가 없는 디버그 도구라 별도
    /// 우선순위 규칙을 두지 않았다).
    /// </summary>
    public class BattleTestUnitPickerView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private BattleFieldWorldCameraView cameraView;

        private Action<BattleTestUnitClickTarget> onUnitClicked;
        private Action<BattleTestSpawnPointMarkerView> onSpawnPointClicked;

        public void Bind(Action<BattleTestUnitClickTarget> onUnitClicked)
        {
            this.onUnitClicked = onUnitClicked;
        }

        public void BindSpawnPointHandler(Action<BattleTestSpawnPointMarkerView> onSpawnPointClicked)
        {
            this.onSpawnPointClicked = onSpawnPointClicked;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cameraView == null) return;

            var worldPos = cameraView.ScreenToWorld(eventData.position);
            var hit = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            if (onUnitClicked != null && hit.TryGetComponent<BattleTestUnitClickTarget>(out var unitTarget))
            {
                onUnitClicked(unitTarget);
            }
            else if (onSpawnPointClicked != null && hit.TryGetComponent<BattleTestSpawnPointMarkerView>(out var marker))
            {
                onSpawnPointClicked(marker);
            }
        }
    }
}
