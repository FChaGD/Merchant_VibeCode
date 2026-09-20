using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// BattleFieldWorldCameraView 전용 커서 고정 줌+드래그팬 계산(순수 C#, MonoBehaviour가
    /// 아닌 이유는 ScrollRectZoomController와 같음 - 테스트 용이성, View 생명주기와 분리). 09번 설계의
    /// ScrollRectZoomController와 같은 규칙(최소 줌=여백 없이 전장이 화면을 채우는 지점, 커서 고정 줌,
    /// 드래그팬)을 Orthographic 카메라 좌표계로 재유도했다(Docs/설계/13번 §7) - RectTransform.localScale은
    /// 클수록 확대지만 Camera.orthographicSize는 작을수록 확대라 관계가 반대다. 공식을 그대로 옮기지 않고
    /// 다시 유도했으니, 이 클래스를 고칠 땐 ScrollRectZoomController를 그대로 베끼지 말 것.
    /// 줌 한계와 이동 범위 규칙은 씬마다 달라서 CameraBehaviors(축별 정책 조합)로 주입받는다
    /// (Docs/설계/27번) - 이 클래스는 상태 소유와 좌표 계산만 하고 씬별 분기를 갖지 않는다.
    /// </summary>
    internal class OrthographicCameraZoomController
    {
        private const float ZoomStep = 0.1f;

        private readonly CameraBehaviors behaviors;
        private Camera targetCamera;
        private float fieldRadius;
        // 전장 전체가 여백 없이 보이는 기준 줌(=orthographicSize). 줌 범위 정책에 따라 "가장 넓게 보이는
        // 줌"이 이 값과 다를 수 있어 minSize가 아니라 baselineSize라 부른다.
        private float baselineSize = 1f;
        private float zoomedInLimit = 1f;
        private float zoomedOutLimit = 1f;
        private float currentSize = 1f;

        public OrthographicCameraZoomController(CameraBehaviors behaviors)
        {
            this.behaviors = behaviors;
        }

        public void Bind(Camera camera)
        {
            targetCamera = camera;
        }

        public float CurrentSize => currentSize;

        /// <summary>
        /// 전장 반지름이 바뀔 때(전투마다) 최소/최대 orthographicSize 경계를 다시 잡는다.
        /// baselineSize = fieldRadius / max(1, aspect) - UGUI 버전의 "여백 없이 화면을 꽉 채우는 최소
        /// 줌"(cover-fit)과 같은 결과를 내도록 재유도한 공식이다: 화면이 가로로 넓을수록(aspect>1)
        /// 세로가 먼저 꽉 차므로 그만큼 더 확대된 상태(작은 size)가 "전장 전체가 보이는" 기준 줌이 된다.
        /// </summary>
        public void RecomputeBounds(float fieldRadius)
        {
            if (targetCamera == null || fieldRadius <= 0f) return;

            this.fieldRadius = fieldRadius;
            baselineSize = fieldRadius / Mathf.Max(1f, targetCamera.aspect);
            var range = behaviors.ZoomRange.Compute(CaptureState());
            zoomedInLimit = range.ZoomedIn;
            zoomedOutLimit = range.ZoomedOut;
            currentSize = Mathf.Clamp(currentSize, zoomedInLimit, zoomedOutLimit);
            ApplySize(currentSize);
            ClampPosition();
        }

        /// <summary>전장 전체가 보이는 기준 줌(=baselineSize) + 중앙 위치로 강제 리셋한다
        /// (전투 시작마다, 기획 09번 §5와 동일 규칙).</summary>
        public void ResetToMinZoom()
        {
            if (targetCamera == null) return;

            currentSize = baselineSize;
            ApplySize(currentSize);
            var pos = targetCamera.transform.position;
            targetCamera.transform.position = new Vector3(0f, 0f, pos.z);
        }

        /// <summary>마우스 커서가 가리키는 월드 지점을 고정한 채 확대/축소한다(기획 09번 §4 커서 앵커 줌).</summary>
        public void ApplyScroll(Vector2 screenPoint, float scrollDeltaY)
        {
            if (targetCamera == null) return;

            // scrollDeltaY>0(휠 위로 굴림)이면 확대(size 감소)해야 하므로 부호를 뒤집는다.
            var newSize = Mathf.Clamp(currentSize - scrollDeltaY * ZoomStep, zoomedInLimit, zoomedOutLimit);
            if (Mathf.Approximately(newSize, currentSize)) return;

            var cursorWorldBefore = ScreenToWorld(screenPoint);

            currentSize = newSize;
            ApplySize(currentSize);

            // 같은 화면 지점이 줌 이후에도 같은 월드 좌표를 가리키도록, 어긋난 만큼 카메라를 되돌린다.
            var cursorWorldAfter = ScreenToWorld(screenPoint);
            var delta = cursorWorldBefore - cursorWorldAfter;
            targetCamera.transform.position += new Vector3(delta.x, delta.y, 0f);
            ClampPosition();
        }

        /// <summary>화면 픽셀 드래그량을 현재 줌 배율 기준 월드 이동량으로 변환해 카메라를 움직인다 -
        /// ScrollRect가 없어 새로 구현하는 부분(§7 재점검 2번, UGUI 버전엔 대응하는 코드가 없었음).</summary>
        public void ApplyDrag(Vector2 screenDelta)
        {
            if (targetCamera == null || Screen.height <= 0) return;

            // 화면 세로 픽셀 1개당 월드 유닛 = (2*orthographicSize)/화면 세로 픽셀 수.
            var worldPerPixel = currentSize * 2f / Screen.height;
            // 드래그는 "손 아래 콘텐츠가 따라온다" 방향이라, 카메라는 반대로 움직여야 같은 효과가 난다
            // (오른쪽으로 드래그하면 콘텐츠가 오른쪽으로 이동한 것처럼 보여야 하므로 카메라는 왼쪽으로).
            var worldDelta = new Vector3(-screenDelta.x, -screenDelta.y, 0f) * worldPerPixel;
            targetCamera.transform.position += worldDelta;
            ClampPosition();
        }

        public Vector3 ScreenToWorld(Vector2 screenPoint)
        {
            var screenPoint3 = new Vector3(screenPoint.x, screenPoint.y, -targetCamera.transform.position.z);
            return targetCamera.ScreenToWorldPoint(screenPoint3);
        }

        private void ApplySize(float size) => targetCamera.orthographicSize = size;

        private CameraViewState CaptureState()
            => new CameraViewState(baselineSize, currentSize, fieldRadius, targetCamera.aspect);

        // 위치 보정 규칙은 이동 범위 정책이 정한다. 카메라 Z는 정책이 모르므로 여기서 유지한다.
        private void ClampPosition()
        {
            if (targetCamera == null) return;

            var pos = targetCamera.transform.position;
            var clamped = behaviors.PanBounds.ClampPosition(new Vector2(pos.x, pos.y), CaptureState());
            targetCamera.transform.position = new Vector3(clamped.x, clamped.y, pos.z);
        }
    }
}
