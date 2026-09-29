using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 그리드 타일 영역. 외곽 판(50 × 50, Docs/기획/59번 §4.3) 전체가 아니라 표시 범위(대열 경계 상자 + 여백, FormationGridEditor가
    /// 계산)의 칸만 슬롯으로 만든다 - 2,500칸을 매번 만들면 UI 오브젝트가 과해진다. 슬롯은 풀로 재사용하고 판 칸 인덱스로 조회한다.
    /// 표시: 민트색 루트 배경 = 외곽 판, 주황 타일 = 대열 칸(FormationSlotView.SetInArea). 기본 화면 = 표시 범위 전체가 들어오는 배율이고,
    /// 이보다 더 축소하지 않는다. 마우스 휠로 확대(칸 최대 slotSize)하고 ScrollRect 드래그로 이동한다(기획 59번 §4.4).
    /// 실제 배치 데이터(FormationLayout)는 FormationGridEditor가 소유하며, 이 클래스는 렌더링과 드래그/드롭 이벤트 중계, Field 배치 시간
    /// 오버레이/경로선(설계 25번 §5) 표시만 담당한다.
    /// </summary>
    public class FormationGridView : MonoBehaviour, IScrollHandler
    {
        // 휠 한 칸당 배율 변화(20%)와 목표 배율까지 따라가는 시간 - 즉시 바꾸면 화면이 끊겨 보여 목표까지 이어서 바꾸고, 5%는 너무 느려 20%로 올렸다
        // (2026-09-29 사용자 요청). 휠 값의 크기는 입력 장치·모듈마다 달라(1 또는 120 등) 방향만 쓴다.
        private const float ZoomStepPerWheel = 0.2f;
        private const float ZoomSmoothTime = 0.08f;
        // 칸 대비 크기 비율 - 원래 120px 칸에 아이콘·오버레이 96px, 이동 아이콘 72px이던 비율을 유지한다. 칸이 확대/축소로 바뀌므로
        // 고정 px로 두면 칸 밖으로 넘치고, 넘친 부분이 뒤에 그려지는 이웃 칸 배경에 가려 잘려 보인다(2026-09-28 실전 확인).
        private const float IconToCellRatio = 0.8f;
        private const float TravelerToCellRatio = 0.6f;

        [SerializeField] private Transform slotContent;
        [SerializeField] private GridLayoutGroup slotLayoutGroup;
        [SerializeField] private FormationSlotView slotPrefab;
        [SerializeField] private FormationUnitIconView occupantIconPrefab;
        // Field 배치 시간(Docs/설계/25번 §5.2) 이동 경로선 - Hub 프리팹에는 없을 수 있어 null 허용.
        [SerializeField] private FormationPathLineView pathLinePrefab;
        // 경로 위를 실시간으로 이동하는 유닛 아이콘(기획 20번 §3.3) - 경로선과 별도 풀로 관리한다.
        [SerializeField] private Image travelerIconPrefab;
        // 배치/이동 진행 중인 슬롯의 반투명 마크(기획 20번 §3.2/§3.3, 설계 25번 §5.1) - 슬롯의
        // 자식이 아니라 이 클래스가 별도 풀로 관리한다.
        [SerializeField] private FormationActivityOverlayView activityOverlayPrefab;
        // 최대 확대 시 칸 크기(기획 59번 §4.4 - 지금까지의 기본 칸 크기).
        [SerializeField] private Vector2 slotSize = new(120f, 120f);
        // 대열 밖 판 칸 색(민트 = 외곽 판, 기획 59번 §4.4). 격자 바탕은 판 밖 색(어두운 회색)이라 칸이 직접 칠한다 - 바탕을 민트로 두면
        // 판 경계 밖 빈 곳까지 판처럼 보인다(2026-09-29 실전 확인, 사용자 결정).
        [SerializeField] private Color boardCellColor = new(0.82f, 0.95f, 0.85f, 1f);

        private readonly List<FormationSlotView> slotPool = new();
        private readonly Dictionary<int, FormationSlotView> slotsByBoardIndex = new();
        private readonly List<FormationPathLineView> pathLines = new();
        private readonly List<Image> travelerIcons = new();
        private readonly List<FormationActivityOverlayView> activityOverlays = new();
        private ScrollRect scrollRect;

        private Action<int> onSlotDropped;
        private Action<IFormationUnit> onIconClicked;
        private Action<int, FormationUnitIconView, PointerEventData> onIconBeginDrag;
        private Action<PointerEventData> onIconDrag;
        private Action<PointerEventData> onIconEndDrag;

        private int boardColumns = FormationLayout.DefaultColumnCount;
        private int boardRows;
        // 배율 기준 범위(대열 + 여백) - 최소 배율(전체 보기)을 정한다. 실제로 칸을 까는 범위(visibleRange)는 이를 뷰포트에 맞춰 늘린 것.
        private RectInt fitRange;
        private RectInt visibleRange;
        private float cellSize;
        private float minCellSize;
        private float zoomTarget;
        private Coroutine zoomRoutine;

        public IEnumerable<int> VisibleSlotIndices => slotsByBoardIndex.Keys;
        public float CellSize => cellSize;
        // 칸 단위로 덧그리는 표시(디버그 외곽선 등)용 - 칸의 자식으로 붙이면 확대/축소·스크롤을 따로 따라갈 필요가 없다.
        public IReadOnlyDictionary<int, FormationSlotView> VisibleSlots => slotsByBoardIndex;
        // 칸 크기·배치가 바뀐 직후(확대/축소, 표시 범위 변경) - 칸 좌표로 직접 그리는 표시(디버그 외곽선)가 다시 그릴 시점을 안다.
        // 드래그 스크롤은 콘텐츠만 움직여 칸 좌표가 그대로이므로 알리지 않는다.
        public event Action ViewChanged;
        // 칸과 함께 스크롤·확대돼야 하는 표시(디버그 핀 등)를 붙일 부모.
        public RectTransform ContentRect => slotContent as RectTransform;

        // 배치된 유닛 아이콘의 현재 월드 크기 - 드래그 고스트를 격자 배율에 맞추는 데 쓴다(고스트는 루트 캔버스 아래라 칸 크기를 직접 모른다).
        public float IconWorldSize => cellSize * IconToCellRatio * (slotContent != null ? slotContent.lossyScale.x : 1f);

        public bool IsVisible(int boardIndex) => slotsByBoardIndex.ContainsKey(boardIndex);

        public void Initialize(
            Action<int> slotDropped,
            Action<IFormationUnit> iconClicked,
            Action<int, FormationUnitIconView, PointerEventData> iconBeginDrag,
            Action<PointerEventData> iconDrag,
            Action<PointerEventData> iconEndDrag)
        {
            onSlotDropped = slotDropped;
            onIconClicked = iconClicked;
            onIconBeginDrag = iconBeginDrag;
            onIconDrag = iconDrag;
            onIconEndDrag = iconEndDrag;
            scrollRect ??= GetComponent<ScrollRect>();
        }

        /// <summary>
        /// 표시 범위와 대열 칸을 반영한다. 슬롯은 풀에서 재사용한다. 열 때(fitView)와 판 자체가 바뀔 때만 "범위 전체 보기"로 맞추고,
        /// 마차 배치 등으로 범위만 바뀌면 사용자가 맞춘 배율과 보던 위치를 유지한다(2026-09-29 사용자 요청 - 배치마다 배율이 리셋되지 않게).
        /// </summary>
        public void SetView(int boardColumnCount, int boardRowCount, RectInt range, RectInt contentRange, Func<int, bool> isInArea, Vector2? focusCenter = null, bool fitView = false)
        {
            StopZoom();
            var boardChanged = boardColumnCount != boardColumns || boardRowCount != boardRows;
            var rangeChanged = boardChanged || !range.Equals(fitRange);
            var keepView = rangeChanged && !boardChanged && cellSize > 0f && !fitView;
            var viewCenter = keepView ? GetViewCenterOnBoard() : Vector2.zero;
            // 지정한 칸 좌표로 화면 중앙을 옮긴다(배율은 유지 - 새 허용 범위로만 보정). 처음 그릴 때는 전체 보기가 이미 범위 중앙에 맞춘다.
            if (focusCenter is { } focus && !boardChanged && cellSize > 0f && !fitView)
            {
                keepView = true;
                viewCenter = focus;
            }
            boardColumns = boardColumnCount;
            boardRows = boardRowCount;
            fitRange = range;
            minCellSize = ComputeFitCellSize();
            // 보던 화면을 유지할 때는 보던 중앙을 기준으로 채운다 - 대열 범위 중앙 기준으로 채우면 최대 축소 상태(스크롤 여지 없음)에서
            // 마차를 옮길 때마다 화면이 대열 중앙으로 따라 움직인다(2026-09-29 실전 확인).
            // 대열이 그대로면(편집 거부 등) 칸 범위를 다시 계산하지 않는다 - 대열 중앙 기준으로 다시 채우면 보던 중앙 기준으로 깔아 둔 창이 바뀌어,
            // 화면이 초기 중앙으로 튀고 재사용된 칸 오브젝트가 다른 판 좌표로 옮겨져 거부 깜빡임이 엉뚱한 칸에 보였다(2026-09-29 실전 확인).
            var reuseVisible = !rangeChanged && !fitView && cellSize > 0f && focusCenter == null;
            visibleRange = reuseVisible
                ? Union(visibleRange, contentRange)
                : Union(ExpandToViewport(range, minCellSize, keepView ? viewCenter : (Vector2?)null), contentRange);

            if (slotPrefab == null || slotContent == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(slotPrefab)} 또는 {nameof(slotContent)}가 지정되어 있지 않다.");
                return;
            }

            range = visibleRange;
            var count = range.width * range.height;
            while (slotPool.Count < count)
            {
                var created = Instantiate(slotPrefab, slotContent);
                // 새 슬롯은 오버레이·경로선보다 앞(아래)에 그려져야 한다 - GridLayoutGroup은 슬롯끼리의 형제 순서로 배치한다.
                created.transform.SetSiblingIndex(slotPool.Count);
                slotPool.Add(created);
            }

            slotsByBoardIndex.Clear();
            for (var i = 0; i < slotPool.Count; i++)
            {
                var slot = slotPool[i];
                if (i >= count)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                // GridLayoutGroup은 활성 자식을 형제 순서대로 좌→우, 위→아래(row-major)로 놓는다.
                var boardIndex = (range.y + i / range.width) * boardColumns + range.x + i % range.width;
                slot.gameObject.SetActive(true);
                slot.Initialize(boardIndex, onSlotDropped);
                slot.SetColors(boardCellColor);
                slot.SetInArea(isInArea(boardIndex));
                slotsByBoardIndex[boardIndex] = slot;
            }

            if (slotLayoutGroup != null) slotLayoutGroup.constraintCount = Mathf.Max(1, range.width);
            if (keepView)
            {
                // 범위가 넓어지면 최소 배율(전체 보기)이 작아지고, 좁아지면 커진다 - 현재 배율을 새 허용 범위 안으로만 보정한다.
                ApplyCellSize(Mathf.Clamp(cellSize, minCellSize, MaxCellSize));
                SetViewCenterOnBoard(viewCenter);
            }
            else if (fitView || rangeChanged || cellSize <= 0f) FitToRange();
            else ApplyCellSize(Mathf.Clamp(cellSize, minCellSize, MaxCellSize));
        }

        // 최소 배율에서 뷰포트를 다 채우도록 표시 범위를 가로·세로로 늘린다(판 경계에서 자름). 배율 기준 범위(대열 + 여백)는 뷰포트 비율과
        // 달라 한 축에 칸 없는 빈 여백이 생기는데, 그 여백이 판 바탕색과 같아 판 칸처럼 보이면서 드롭에 반응하지 않았다(2026-09-29 실전 확인,
        // 사용자 결정 - 여백을 실제 판 칸으로 채움). 최소 배율에서 채우면 확대한 배율에서도 늘 뷰포트보다 넓다.
        // keepCenter가 있으면 그 칸 좌표를 중앙으로 한 뷰포트 크기 창을 깔고, 배율 기준 범위가 창을 벗어나면 그만큼 더 넓힌다 - 그래야 보던 중앙을
        // 그대로 둘 수 있다(범위가 창보다 넓어지면 스크롤할 수 있게 된다).
        private RectInt ExpandToViewport(RectInt range, float minCell, Vector2? keepCenter)
        {
            var viewport = ViewportSize;
            if (minCell <= 0f || viewport.x <= 0f || viewport.y <= 0f) return range;

            var (x, width) = Expand(range.x, range.width, Mathf.CeilToInt(viewport.x / minCell), boardColumns, keepCenter?.x);
            var (y, height) = Expand(range.y, range.height, Mathf.CeilToInt(viewport.y / minCell), boardRows, keepCenter?.y);
            return new RectInt(x, y, width, height);

            static (int start, int length) Expand(int start, int length, int wanted, int boardLength, float? center)
            {
                if (center is { } c)
                {
                    var windowStart = Mathf.FloorToInt(c - wanted * 0.5f);
                    var unionStart = Mathf.Max(0, Mathf.Min(windowStart, start));
                    var unionEnd = Mathf.Min(boardLength, Mathf.Max(windowStart + wanted, start + length));
                    return (unionStart, Mathf.Max(0, unionEnd - unionStart));
                }

                var target = Mathf.Min(Mathf.Max(length, wanted), Mathf.Max(length, boardLength));
                var newStart = start - (target - length) / 2;
                newStart = Mathf.Clamp(newStart, 0, Mathf.Max(0, boardLength - target));
                return (newStart, target);
            }
        }

        // 칸을 까는 범위는 최대 축소 범위보다 넓을 수 있다(마을의 연결 가능 칸 여백) - 그 칸은 확대해 스크롤로 접근한다.
        private static RectInt Union(RectInt a, RectInt b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            return new RectInt(xMin, yMin, Mathf.Max(a.xMax, b.xMax) - xMin, Mathf.Max(a.yMax, b.yMax) - yMin);
        }

        // 유닛 유무와 무관하게 슬롯당 아이콘 인스턴스 하나를 계속 재사용한다(파괴 후 재생성 대신
        // Bind로 내용을 덮어쓰고 SetActive로 표시만 전환) - CLAUDE.md의 "슬롯/아이콘 렌더링은 매번
        // Destroy+Instantiate하지 않고 get-or-create로 재사용한다" 규칙을 따른다.
        public void RenderSlot(int boardIndex, IFormationUnit unit)
        {
            if (!slotsByBoardIndex.TryGetValue(boardIndex, out var slot))
            {
                return;
            }

            if (unit == null)
            {
                if (slot.CurrentIcon != null)
                {
                    slot.CurrentIcon.gameObject.SetActive(false);
                }
                return;
            }

            if (occupantIconPrefab == null)
            {
                Debug.LogWarning($"{nameof(FormationGridView)}에 {nameof(occupantIconPrefab)}가 지정되어 있지 않다.");
                return;
            }

            if (slot.CurrentIcon == null)
            {
                slot.SetIcon(Instantiate(occupantIconPrefab, slot.IconContainer));
                // 아이콘을 칸에 비례하게 늘린다(가운데 80%) - 칸 크기가 바뀌어도 레이아웃이 자동으로 따라간다.
                var iconRect = (RectTransform)slot.CurrentIcon.transform;
                var inset = (1f - IconToCellRatio) * 0.5f;
                iconRect.anchorMin = new Vector2(inset, inset);
                iconRect.anchorMax = new Vector2(1f - inset, 1f - inset);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }

            var icon = slot.CurrentIcon;
            icon.gameObject.SetActive(true);
            icon.Bind(unit);

            icon.SetHandlers(
                _ => onIconClicked?.Invoke(unit),
                (iconView, eventData) => onIconBeginDrag?.Invoke(slot.SlotIndex, iconView, eventData),
                eventData => onIconDrag?.Invoke(eventData),
                eventData => onIconEndDrag?.Invoke(eventData));
        }

        /// <summary>제거·이동이 거부된 칸을 짧게 붉게 깜빡인다(설계 60번 §11-5).</summary>
        public void FlashRejected(int boardIndex)
        {
            if (slotsByBoardIndex.TryGetValue(boardIndex, out var slot)) slot.FlashRejected();
        }

        // 슬롯의 현재 화면 좌표(slotContent 기준 anchoredPosition) - 이동 경로선(§5.2)과 오버레이
        // 마크(§5.1)가 여기 기준으로 자기 위치를 잡는다. GridLayoutGroup이 계산해 둔 실제 배치 좌표를 그대로 읽는다.
        // 표시 범위 밖 칸은 그릴 일이 없다(경로·활동은 대열 안에서만 일어난다).
        public Vector2 GetSlotAnchoredPosition(int boardIndex)
            => slotsByBoardIndex.TryGetValue(boardIndex, out var slot) ? ((RectTransform)slot.transform).anchoredPosition : Vector2.zero;

        // 확대/축소(기획 59번 §4.4) - 최소 = 범위 전체 보기, 최대 = slotSize. 스크롤 위치는 비율로 유지한다.
        public void OnScroll(PointerEventData eventData)
        {
            if (cellSize <= 0f || Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;

            // 애니메이션 중에 휠을 더 굴리면 현재 값이 아니라 목표 값에서 이어서 누적한다.
            var from = zoomRoutine != null ? zoomTarget : cellSize;
            var factor = eventData.scrollDelta.y > 0f ? 1f + ZoomStepPerWheel : 1f / (1f + ZoomStepPerWheel);
            zoomTarget = Mathf.Clamp(from * factor, minCellSize, MaxCellSize);
            if (Mathf.Approximately(zoomTarget, cellSize)) return;

            zoomRoutine ??= StartCoroutine(ZoomRoutine());
        }

        // 목표 배율까지 지수적으로 따라간다. 매 단계 화면 중앙이 보던 칸을 유지한다(정규화 스크롤 위치로 유지하면 콘텐츠 크기가 바뀌며 중앙이 밀린다).
        // 입력은 휠 이벤트로 받고, 이 코루틴은 애니메이션 중에만 돈다(상시 Update 폴링 아님).
        private IEnumerator ZoomRoutine()
        {
            while (!Mathf.Approximately(cellSize, zoomTarget))
            {
                var center = GetViewCenterOnBoard();
                var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / ZoomSmoothTime);
                var next = Mathf.Abs(zoomTarget - cellSize) < 0.1f ? zoomTarget : Mathf.Lerp(cellSize, zoomTarget, t);
                ApplyCellSize(next);
                SetViewCenterOnBoard(center);
                yield return null;
            }
            zoomRoutine = null;
        }

        // 표시 범위가 바뀌는 등 배율을 코드로 정할 때는 진행 중인 휠 애니메이션을 멈춘다 - 멈추지 않으면 새 배율을 옛 목표로 덮어쓴다.
        private void StopZoom()
        {
            if (zoomRoutine != null) StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }

        private void OnDisable() => zoomRoutine = null;

        // 배치/이동 진행 중인 슬롯마다 반투명 마크(기획 20번 §3.2/§3.3)를 하나씩 그린다 - 개수가
        // 줄면 남는 인스턴스는 숨기기만 하고 파괴하지 않는다(get-or-create 재사용). 렌더 순서는
        // 호출 순서로 정한다 - 반드시 SetPathLines 다음, SetTravelerIcons 이전에 호출할 것
        // (FormationGridEditor.TickActivityOverlays 참고, 실전 확인된 순서: 슬롯 < 경로선 < 이
        // 마크 < 이동 아이콘).
        public void SetActivityOverlays(IReadOnlyList<FormationActivityOverlayVisual> overlays,
            Action<string, PointerEventData> onGhostBeginDrag = null, Action<PointerEventData> onGhostDrag = null, Action<PointerEventData> onGhostEndDrag = null)
        {
            if (activityOverlayPrefab == null)
            {
                return;
            }

            while (activityOverlays.Count < overlays.Count)
            {
                activityOverlays.Add(Instantiate(activityOverlayPrefab, slotContent));
            }

            for (var i = 0; i < activityOverlays.Count; i++)
            {
                activityOverlays[i].transform.SetAsLastSibling();
                if (i < overlays.Count)
                {
                    var overlay = overlays[i];
                    var view = activityOverlays[i];
                    view.gameObject.SetActive(true);
                    var overlayRect = (RectTransform)view.transform;
                    overlayRect.anchoredPosition = GetSlotAnchoredPosition(overlay.SlotIndex);
                    overlayRect.sizeDelta = Vector2.one * (cellSize * IconToCellRatio);
                    view.Bind(overlay.Unit?.Icon, overlay.Activity.RequiredSeconds - overlay.Activity.ElapsedSeconds,
                        overlay.IsRedirectableTarget, overlay.Activity.UnitId, onGhostBeginDrag, onGhostDrag, onGhostEndDrag,
                        showRemainingSeconds: !overlay.IsMoveOrigin);
                }
                else
                {
                    activityOverlays[i].gameObject.SetActive(false);
                }
            }
        }

        // 동시에 진행 중인 이동 활동 수만큼 경로선을 그린다 - 개수가 줄면 남는 인스턴스는 숨기기만 하고
        // 파괴하지 않는다. 반드시 SetActivityOverlays 이전에 호출할 것(렌더 순서, 위 주석 참고).
        public void SetPathLines(IReadOnlyList<FormationMovePathVisual> moves)
        {
            if (pathLinePrefab == null)
            {
                return;
            }

            while (pathLines.Count < moves.Count)
            {
                pathLines.Add(Instantiate(pathLinePrefab, slotContent));
            }

            for (var i = 0; i < pathLines.Count; i++)
            {
                pathLines[i].transform.SetAsLastSibling();
                if (i < moves.Count)
                {
                    pathLines[i].gameObject.SetActive(true);
                    pathLines[i].SetPath(BuildWaypoints(moves[i]));
                }
                else
                {
                    pathLines[i].gameObject.SetActive(false);
                }
            }
        }

        // 슬롯 좌표 목록을 만든 뒤, 부분 구간(리다이렉트 직후 연속 좌표, 기획 21번 §2)이 있으면 그
        // 지점 하나만 정확한 위치로 치환한다 - SetPathLines/SetTravelerIcons가 공유한다.
        private List<Vector2> BuildWaypoints(FormationMovePathVisual move)
        {
            var positions = new List<Vector2>(move.PathSlotIndices.Count);
            foreach (var slotIndex in move.PathSlotIndices)
            {
                positions.Add(GetSlotAnchoredPosition(slotIndex));
            }
            FormationPathInterpolation.ApplyPartialSegment(positions, move.PartialSegmentIndex, move.PartialSegmentWeight);
            return positions;
        }

        // 경로 위를 실시간으로 이동하는 유닛 아이콘을 그린다(기획 20번 §3.3) - 반드시
        // SetActivityOverlays 이후에 호출할 것(항상 모든 것보다 위, 렌더 순서 위 주석 참고).
        public void SetTravelerIcons(IReadOnlyList<FormationMovePathVisual> moves)
        {
            if (travelerIconPrefab == null)
            {
                return;
            }

            while (travelerIcons.Count < moves.Count)
            {
                travelerIcons.Add(Instantiate(travelerIconPrefab, slotContent));
            }

            for (var i = 0; i < travelerIcons.Count; i++)
            {
                travelerIcons[i].transform.SetAsLastSibling();
                if (i < moves.Count)
                {
                    var move = moves[i];
                    var positions = BuildWaypoints(move);
                    // 대각선 구간(√2배, 설계 26번 §10)이 섞일 수 있어 균등 보간이 아니라 구간별
                    // 실제 비용 배열로 보간한다.
                    var weights = FormationPathFinder.ComputeSegmentWeights(move.PathSlotIndices, boardColumns, move.PartialSegmentIndex, move.PartialSegmentWeight);
                    var traveler = travelerIcons[i];
                    traveler.gameObject.SetActive(true);
                    traveler.sprite = move.Icon;
                    traveler.enabled = move.Icon != null;
                    var travelerRect = (RectTransform)traveler.transform;
                    travelerRect.anchoredPosition = FormationPathInterpolation.Evaluate(positions, move.Progress01, weights);
                    travelerRect.sizeDelta = Vector2.one * (cellSize * TravelerToCellRatio);
                }
                else
                {
                    travelerIcons[i].gameObject.SetActive(false);
                }
            }
        }

        private float MaxCellSize => Mathf.Max(1f, Mathf.Min(slotSize.x, slotSize.y));

        // 표시 범위 전체가 뷰포트에 들어오는 칸 크기(= 최소 배율). 범위가 작으면 최대 칸 크기에서 멈춘다.
        private void FitToRange()
        {
            ApplyCellSize(minCellSize);
            // 표시 범위가 판 경계에서 잘리면 배율 기준 범위가 표시 범위 가운데에 오지 않는다 - 스크롤 중앙이 아니라 기준 범위 중앙을 맞춘다.
            SetViewCenterOnBoard(fitRange.center);
        }

        private float ComputeFitCellSize()
        {
            var viewport = ViewportSize;
            var fit = Mathf.Min(viewport.x / Mathf.Max(1, fitRange.width), viewport.y / Mathf.Max(1, fitRange.height));
            return Mathf.Min(Mathf.Max(1f, fit), MaxCellSize);
        }

        private Vector2 ViewportSize => scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : ((RectTransform)transform).rect.size;

        // 뷰포트 중앙이 가리키는 판 좌표(칸 단위, 소수 포함, y는 아래로 증가) - 범위가 바뀐 뒤 같은 곳을 계속 보이게 하려고 칸 좌표로 기억한다.
        // 범위가 바뀌면 콘텐츠 크기·여백이 달라져 정규화 스크롤 위치를 그대로 두면 보던 곳이 밀린다.
        private Vector2 GetViewCenterOnBoard()
        {
            var viewport = ViewportSize;
            var content = ContentSize(visibleRange, cellSize);
            var padding = slotLayoutGroup != null ? new Vector2(slotLayoutGroup.padding.left, slotLayoutGroup.padding.top) : Vector2.zero;
            var normalized = scrollRect != null ? scrollRect.normalizedPosition : new Vector2(0.5f, 0.5f);
            var offsetX = Mathf.Max(0f, content.x - viewport.x) * normalized.x;
            var offsetFromTop = Mathf.Max(0f, content.y - viewport.y) * (1f - normalized.y);
            return new Vector2(
                visibleRange.x + (offsetX + viewport.x * 0.5f - padding.x) / cellSize,
                visibleRange.y + (offsetFromTop + viewport.y * 0.5f - padding.y) / cellSize);
        }

        private void SetViewCenterOnBoard(Vector2 boardCenter)
        {
            if (scrollRect == null) return;

            var viewport = ViewportSize;
            var content = ContentSize(visibleRange, cellSize);
            var padding = slotLayoutGroup != null ? new Vector2(slotLayoutGroup.padding.left, slotLayoutGroup.padding.top) : Vector2.zero;
            var scrollableX = content.x - viewport.x;
            var scrollableY = content.y - viewport.y;
            var offsetX = (boardCenter.x - visibleRange.x) * cellSize + padding.x - viewport.x * 0.5f;
            var offsetFromTop = (boardCenter.y - visibleRange.y) * cellSize + padding.y - viewport.y * 0.5f;
            scrollRect.normalizedPosition = new Vector2(
                scrollableX > 0f ? Mathf.Clamp01(offsetX / scrollableX) : 0.5f,
                scrollableY > 0f ? 1f - Mathf.Clamp01(offsetFromTop / scrollableY) : 0.5f);
        }

        // 여백 포함 콘텐츠 크기 - ApplyCellSize의 여백 규칙(뷰포트보다 작으면 가운데 정렬 여백)과 같은 계산.
        private Vector2 ContentSize(RectInt range, float size)
        {
            var viewport = ViewportSize;
            return new Vector2(Mathf.Max(viewport.x, range.width * size), Mathf.Max(viewport.y, range.height * size));
        }

        // 칸 크기를 바꾸고, 타일이 뷰포트보다 작으면 여백(padding)으로 가운데 정렬한다 - ScrollRect는 콘텐츠를 뷰포트 좌상단에 붙이므로
        // 여백 없이 두면 범위 전체 보기 상태에서 타일이 한쪽에 몰린다. 슬롯 좌표를 오버레이가 바로 읽을 수 있게 즉시 다시 배치한다.
        private void ApplyCellSize(float size)
        {
            cellSize = size;
            if (slotLayoutGroup == null) return;

            slotLayoutGroup.cellSize = new Vector2(size, size);
            slotLayoutGroup.spacing = Vector2.zero;
            var viewport = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : Vector2.zero;
            var padX = Mathf.Max(0, Mathf.FloorToInt((viewport.x - visibleRange.width * size) * 0.5f));
            var padY = Mathf.Max(0, Mathf.FloorToInt((viewport.y - visibleRange.height * size) * 0.5f));
            slotLayoutGroup.padding = new RectOffset(padX, padX, padY, padY);

            if (slotContent is RectTransform contentRect) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            ViewChanged?.Invoke();
        }
    }
}
