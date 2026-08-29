using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 전투 뷰 배경(전체 화면 Image, raycastTarget=true)에서 드래그팬/휠줌 입력을 받아
    /// BattleFieldWorldCameraView로 그대로 전달한다(Docs/설계/13번 §4) - 렌더링은 월드로 옮겼지만
    /// 입력 캡처는 기존 UGUI EventSystem을 그대로 재사용한다(CLAUDE.md 최적화 규칙 - Update() 폴링
    /// 대신 이벤트/콜백). 대상 카메라 뷰는 Camera.main에서 직접 찾는다 - Main Camera를 재사용하기로
    /// 확정했으므로(§6) 별도 씬 마커 없이도 안전하게 조회 가능하다.
    /// </summary>
    public class BattleFieldInputForwarder : MonoBehaviour, IDragHandler, IScrollHandler
    {
        private BattleFieldWorldCameraView cameraView;

        private void Awake()
        {
            cameraView = Camera.main != null ? Camera.main.GetComponent<BattleFieldWorldCameraView>() : null;
        }

        public void OnDrag(PointerEventData eventData) => cameraView?.ApplyDrag(eventData.delta);

        public void OnScroll(PointerEventData eventData) => cameraView?.ApplyScroll(eventData.position, eventData.scrollDelta.y);
    }
}
