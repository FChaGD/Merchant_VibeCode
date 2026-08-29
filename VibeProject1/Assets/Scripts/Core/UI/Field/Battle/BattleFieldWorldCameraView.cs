using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 뷰 카메라(월드 오브젝트 전환, Docs/설계/13_전투뷰_월드오브젝트_전환_아키텍처.md) - 새
    /// 카메라를 만들지 않고 Field 씬의 기존 Main Camera에 부착된다(§6 확정 - 이미 Orthographic이고
    /// AudioListener도 있어 재사용이 더 안전함). 드래그팬/휠줌 "입력 캡처"는
    /// BattleFieldInputForwarder(UGUI EventSystem 경유)가 담당하고, 이 클래스는 실제 카메라 조작
    /// (OrthographicCameraZoomController 위임)만 담당한다 - 렌더링만 월드로 옮기고 입력 경로는 기존
    /// EventSystem 인프라를 재사용하는 절충이다.
    /// </summary>
    public class BattleFieldWorldCameraView : MonoBehaviour
    {
        private const float MaxZoomRatio = 2.5f; // 기획 09번 §3.2 확정값 - UGUI 버전과 동일 규칙.

        private OrthographicCameraZoomController zoomController;

        private void Awake()
        {
            var battleCamera = GetComponent<Camera>();
            // 재사용하는 Main Camera가 이미 Orthographic이지만(§6 확인됨), 씬 설정이 실수로 바뀌어도
            // 이 뷰가 스스로 강제해 조용히 깨지지 않게 한다.
            battleCamera.orthographic = true;
            zoomController = new OrthographicCameraZoomController(MaxZoomRatio);
            zoomController.Bind(battleCamera);
        }

        /// <summary>
        /// 전투 시작마다 BattleViewPresenter가 호출한다 - 전장 반지름이 대형 크기마다 달라지므로 경계를
        /// 다시 잡고, 이전 전투의 팬/줌 상태를 이어받지 않도록 최소 줌+중앙으로 강제 리셋한다(기획 09번 §5).
        /// </summary>
        public void ConfigureFieldBounds(float fieldRadius)
        {
            zoomController.RecomputeBounds(fieldRadius);
            zoomController.ResetToMinZoom();
        }

        public void ApplyScroll(Vector2 screenPoint, float scrollDeltaY) => zoomController.ApplyScroll(screenPoint, scrollDeltaY);

        public void ApplyDrag(Vector2 screenDelta) => zoomController.ApplyDrag(screenDelta);
    }
}
