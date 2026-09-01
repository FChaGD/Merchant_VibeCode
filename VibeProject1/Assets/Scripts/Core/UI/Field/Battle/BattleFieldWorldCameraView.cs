using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 뷰 카메라(월드 오브젝트 전환, Docs/설계/13-2026-08-29-전투뷰_월드오브젝트_전환_아키텍처.md) - 새
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

        // 배틀 테스트 씬의 유닛 팔레트 드래그-드롭이 놓는 지점(화면 좌표)을 전장 월드 좌표로 바꿀 때
        // 쓴다 - 순수 추가, 기존 팬/줌 동작에는 영향 없음.
        public Vector2 ScreenToWorld(Vector2 screenPoint) => zoomController.ScreenToWorld(screenPoint);

        // 배틀 테스트 씬의 유닛 팔레트 드래그 고스트 크기를 현재 줌 배율에 동기화할 때 쓴다 - 순수
        // 접근성 확장, 기존 팬/줌 동작에는 영향 없음.
        public float CurrentOrthographicSize => zoomController.CurrentSize;

        // 배틀 테스트 씬 전용 - 기준 줌보다 더 넓게 줌아웃할 수 있게 범위를 넓히고(요구사항: 기준의
        // 3배까지 넓게, 1/2배까지 확대), 전장 밖으로 드래그팬이 막히는 제약도 없앨 수 있다
        // (clampToField=false). Field 씬은 이 메서드를 호출하지 않으므로 Awake()가 만든 기본
        // 컨트롤러(zoomOutRatio=1, clampToField=true, 기존 동작)를 그대로 쓴다 - 순수 추가, 실제
        // 게임 영향 없음.
        public void ConfigureZoomRange(float zoomInRatio, float zoomOutRatio, bool clampToField = true)
        {
            zoomController = new OrthographicCameraZoomController(zoomInRatio, zoomOutRatio, clampToField);
            zoomController.Bind(GetComponent<Camera>());
        }
    }
}
