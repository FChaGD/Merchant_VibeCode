using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// BattleFieldWorldCameraView 전용 커서 고정 줌+드래그팬+경계 클램프 계산(순수 C#, MonoBehaviour가
    /// 아닌 이유는 ScrollRectZoomController와 같음 - 테스트 용이성, View 생명주기와 분리). 09번 설계의
    /// ScrollRectZoomController와 같은 규칙(최소 줌=여백 없이 전장이 화면을 채우는 지점, 최대 줌=최소
    /// 줌×2.5, 커서 고정 줌, 드래그팬, 경계 하드 클램프)을 Orthographic 카메라 좌표계로 재유도했다
    /// (Docs/설계/13번 §7) - RectTransform.localScale은 클수록 확대지만 Camera.orthographicSize는
    /// 작을수록 확대라 관계가 반대다. 공식을 그대로 옮기지 않고 다시 유도했으니, 이 클래스를 고칠 땐
    /// ScrollRectZoomController를 그대로 베끼지 말 것.
    /// </summary>
    internal class OrthographicCameraZoomController
    {
        private const float ZoomStep = 0.1f;

        private readonly float zoomInRatio;
        // 배틀 테스트 씬 전용 확장 - 기본값 1이면 기존 동작과 완전히 동일하다(베이스라인보다 더 못
        // 넓힘). Field 씬은 이 값을 바꿀 방법 자체가 없어(BattleFieldWorldCameraView.Awake가 고정
        // 생성자만 씀) 영향이 없다.
        private readonly float zoomOutRatio;
        // 배틀 테스트 씬 전용 확장 - true(기본값)면 기존 동작 그대로(전장 정사각형 밖으로 못 나감).
        // 테스트 씬은 이 제약 자체를 없애고 싶다는 요구라 false로 구성해 ClampPosition의 X/Y 클램프를
        // 건너뛴다. Field 씬은 이 값을 바꿀 방법이 없어 영향이 없다.
        private readonly bool clampToField;
        private Camera targetCamera;
        private float fieldRadius;
        // 전장 전체가 여백 없이 보이는 기준 줌(=orthographicSize). 이름을 minSize→baselineSize로
        // 바꿨다 - zoomOutRatio 도입으로 "최소 줌(가장 넓게)"이 더 이상 이 값이 아니게 됐다.
        private float baselineSize = 1f;
        private float zoomedInLimit = 1f;
        private float zoomedOutLimit = 1f;
        private float currentSize = 1f;

        public OrthographicCameraZoomController(float zoomInRatio, float zoomOutRatio = 1f, bool clampToField = true)
        {
            this.zoomInRatio = zoomInRatio;
            this.zoomOutRatio = Mathf.Max(1f, zoomOutRatio);
            this.clampToField = clampToField;
        }

        public void Bind(Camera camera)
        {
            targetCamera = camera;
        }

        // 배틀 테스트 씬의 유닛 팔레트 드래그 고스트가 현재 줌 배율에 맞춰 자기 크기를 계산할 때 쓴다 -
        // 순수 접근성 확장, 기존 팬/줌 동작에는 영향 없음.
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
            var aspect = targetCamera.aspect;
            baselineSize = fieldRadius / Mathf.Max(1f, aspect);
            // orthographicSize는 작을수록 확대이므로, UGUI 버전의 "minZoom*ratio"(곱하기)가 아니라
            // 나누기로 더 작은(더 확대된) 값을 얻는다.
            zoomedInLimit = baselineSize / zoomInRatio;
            zoomedOutLimit = baselineSize * zoomOutRatio;
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

        // 배틀 테스트 씬의 팔레트 드래그-드롭(화면 좌표 → 전장 월드 좌표 변환)이 재사용할 수 있도록
        // BattleFieldWorldCameraView.ScreenToWorld를 통해 노출한다(순수 접근성 확장, 계산 로직 불변).
        public Vector3 ScreenToWorld(Vector2 screenPoint)
        {
            var screenPoint3 = new Vector3(screenPoint.x, screenPoint.y, -targetCamera.transform.position.z);
            return targetCamera.ScreenToWorldPoint(screenPoint3);
        }

        private void ApplySize(float size) => targetCamera.orthographicSize = size;

        // 카메라가 보는 범위가 전장 정사각형(중심 원점, 한 변 fieldRadius*2) 밖으로 나가지 않게 위치를
        // 클램프한다 - UGUI 버전의 ClampPosition과 대응되지만, 콘텐츠가 아니라 카메라 시야 자체를
        // 경계 안에 가두는 반대 방향 계산이다.
        private void ClampPosition()
        {
            if (targetCamera == null || !clampToField) return;

            var halfHeight = currentSize;
            var halfWidth = currentSize * targetCamera.aspect;
            var maxOffsetX = Mathf.Max(fieldRadius - halfWidth, 0f);
            var maxOffsetY = Mathf.Max(fieldRadius - halfHeight, 0f);

            var pos = targetCamera.transform.position;
            pos.x = Mathf.Clamp(pos.x, -maxOffsetX, maxOffsetX);
            pos.y = Mathf.Clamp(pos.y, -maxOffsetY, maxOffsetY);
            targetCamera.transform.position = pos;
        }
    }
}
