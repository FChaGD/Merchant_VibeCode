using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ScrollRect(Clamped) 기반 드래그팬+휠줌 View가 공유하는 줌 계산 로직 - 커서가 가리키는 지점을
    /// 고정한 채 확대/축소하고, 최소 줌(콘텐츠 전체가 뷰포트에 들어오는 지점)/최대 줌(최소 줌 배수)
    /// 경계를 계산해 클램핑한다(Docs/기획/09_전투뷰_카메라_기획.md §3/§4). MonoBehaviour가 아닌 순수
    /// C# 객체로 두어 View의 생명주기와 분리한다(FieldCameraController/BattleFieldLayout과 같은 이유).
    /// TripMapView(콘텐츠 크기 고정, 최초 1회)가 RecomputeBounds를 호출해 쓴다. 전투 뷰는 월드
    /// 오브젝트로 전환되며(Docs/설계/13번) 더 이상 이 클래스를 쓰지 않는다 - 같은 규칙을
    /// Orthographic 카메라 좌표계로 재유도한 OrthographicCameraZoomController를 대신 쓴다(관계가
    /// 반대라 공식을 그대로 옮길 수 없었다 - 13번 §7 참고).
    /// 드래그(팬) 자체는 이 클래스가 아니라 Unity 표준 ScrollRect가 전담한다(SRP) - 여기서는 줄과,
    /// 줌이 만들어낸 위치를 다시 경계 안으로 클램핑하는 것만 책임진다.
    /// </summary>
    internal class ScrollRectZoomController
    {
        private const float ZoomStep = 0.1f;

        private readonly float maxZoomRatio;
        private RectTransform viewport;
        private RectTransform content;
        private float minZoom = 1f;
        private float maxZoom = 1f;
        private float currentZoom = 1f;

        public ScrollRectZoomController(float maxZoomRatio)
        {
            this.maxZoomRatio = maxZoomRatio;
        }

        public void Bind(RectTransform viewport, RectTransform content)
        {
            this.viewport = viewport;
            this.content = content;
        }

        /// <summary>콘텐츠/뷰포트 크기가 바뀐 뒤(최초 1회 또는 전투마다) 최소/최대 줌 경계를 다시 잡는다.</summary>
        public void RecomputeBounds()
        {
            if (viewport == null || content == null || content.rect.width <= 0f || content.rect.height <= 0f) return;

            // 두 축(가로/세로) 중 더 제약이 큰(더 큰) 배율을 써야 반대편 축에도 빈 여백이 생기지 않는다
            // (contain-fit - 콘텐츠 전체가 뷰포트 안에 들어오는 최소 줌).
            minZoom = Mathf.Max(viewport.rect.width / content.rect.width, viewport.rect.height / content.rect.height);
            maxZoom = minZoom * maxZoomRatio;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            ApplyZoom(currentZoom);
            ClampPosition();
        }

        /// <summary>전장 전체가 보이는 최소 줌 + 중앙 위치로 강제 리셋한다(전투 시작마다, 기획 §5).</summary>
        public void ResetToMinZoom()
        {
            currentZoom = minZoom;
            ApplyZoom(currentZoom);
            content.anchoredPosition = Vector2.zero;
        }

        /// <summary>마우스 커서가 가리키는 화면 지점을 고정한 채 확대/축소한다(기획 §4 커서 앵커 줌).</summary>
        public void ApplyScroll(Vector2 screenPoint, float scrollDeltaY, Camera eventCamera)
        {
            if (viewport == null || content == null) return;

            var newZoom = Mathf.Clamp(currentZoom + scrollDeltaY * ZoomStep, minZoom, maxZoom);
            if (Mathf.Approximately(newZoom, currentZoom)) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPoint, eventCamera, out var cursorInViewport);
            // 줌 전 스케일 기준으로 커서 아래에 있던 콘텐츠 로컬 좌표를 구해 둔다.
            var cursorInContent = (cursorInViewport - content.anchoredPosition) / currentZoom;

            currentZoom = newZoom;
            ApplyZoom(currentZoom);
            // 같은 콘텐츠 좌표가 새 스케일에서도 커서 아래 같은 화면 지점에 오도록 위치를 역산한다.
            content.anchoredPosition = cursorInViewport - cursorInContent * currentZoom;
            ClampPosition();
        }

        private void ApplyZoom(float zoom) => content.localScale = new Vector3(zoom, zoom, 1f);

        private void ClampPosition()
        {
            var scaledContentSize = content.rect.size * currentZoom;
            var maxOffset = Vector2.Max((scaledContentSize - viewport.rect.size) * 0.5f, Vector2.zero);
            var position = content.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -maxOffset.x, maxOffset.x);
            position.y = Mathf.Clamp(position.y, -maxOffset.y, maxOffset.y);
            content.anchoredPosition = position;
        }
    }
}
