using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using Game.Core.DebugTools;
#endif
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI의 그리드/팔레트 렌더링과 드래그 이벤트 중계를 전담하는 공용 로직(Docs/설계/25번 §2.2) -
    /// Hub/Field 배치 UI(HubFormationPanel/FieldFormationPanel)가 공유한다. "드롭 시 무엇을 반영할지"
    /// (교체/스왑/타이머 시작 등)는 이 클래스가 판단하지 않고 IFormationEditingHandler에 위임한다 -
    /// 이 클래스는 화면 갱신과 이벤트 중계만 담당한다(SRP).
    /// 화면은 외곽 판 전체가 아니라 대열 영역(FormationArea)의 경계 상자 + 여백만 그린다(Docs/기획/59번 §4.4, 설계 60번 §6). 대열이 편집마다
    /// 바뀌므로 드래그가 끝날 때마다 영역·표시 범위를 다시 계산해 전체를 다시 그린다.
    /// </summary>
    internal class FormationGridEditor
    {
        private readonly IFormationEditingHandler handler;
        // "배경 진행 활동"은 Field에만 있는 개념이라(IFormationActivityHandler, ISP) Hub 핸들러는
        // 이 인터페이스를 구현하지 않는다 - 캐스팅 결과가 null이면 아래 각 사용처가 "활동 없음"으로
        // 취급한다(Docs/Refactor/2026-09-08_공통.md §6.3 수정 G).
        private readonly IFormationActivityHandler activityHandler;
        private readonly FormationDragCoordinator dragCoordinator = new();

        private GameObject panelRoot;
        private FormationPaletteView paletteView;
        private FormationGridView gridView;
        private FormationInfoPanelView infoPanelView;
#if UNITY_EDITOR
        // 디버그 핀 패널 연동 지점(설계 60번 §8) - 디버그 도구를 걷어낼 때는 이 #if UNITY_EDITOR 블록들도 함께 지운다.
        private FormationGridDebugView debugView;
#endif
        // 대열 영역 외곽선(디버그 도구, 마을 정비창만 넘겨줌) - 없으면 null.
        private IFormationAreaOutline areaOutline;

        private ICaravanRosterProvider rosterProvider;
        private IUnitConditionRepository conditionRepository;

        private FormationLayout displayLayout;
        // 마차가 없어질 때(또는 마차 없이 열 때) 기준 칸으로 화면을 옮기기 위한 직전 마차 수. -1 = 방금 열림.
        private int lastWagonCount = -1;
        private readonly Dictionary<string, IFormationUnit> unitsById = new();
        private IReadOnlyList<IFormationUnit> currentRoster = Array.Empty<IFormationUnit>();

        // TickActivityOverlays가 매 프레임 호출되므로(Field 배치 UI가 열려 있는 동안) 매번 새로
        // 할당하지 않고 이 두 버퍼를 Clear()해서 재사용한다(최적화, Docs/Refactor/2026-09-08_공통.md
        // §6.3 수정 F).
        private readonly List<FormationActivityOverlayVisual> activityOverlayBuffer = new();
        private readonly List<FormationMovePathVisual> movePathBuffer = new();

        // 표시 범위 = 대열 경계 상자 바깥 이 칸 수만큼(설계 60번 §11-3).

        public GameObject PanelRoot => panelRoot;

        public FormationGridEditor(IFormationEditingHandler handler)
        {
            this.handler = handler;
            activityHandler = handler as IFormationActivityHandler;
        }

        public bool TryBind(SceneUIRoot sceneUIRoot, FormationUnitIconView dragGhostPrefab)
        {
            if (!sceneUIRoot.TryGetElement<Transform>(FormationUIElementIds.PanelRoot, out var rootTransform))
            {
                WarnMissing(FormationUIElementIds.PanelRoot);
                return false;
            }
            panelRoot = rootTransform.gameObject;

            if (!sceneUIRoot.TryGetElement<FormationPaletteView>(FormationUIElementIds.PaletteRoot, out paletteView))
            {
                WarnMissing(FormationUIElementIds.PaletteRoot);
                return false;
            }

#if UNITY_EDITOR
            if (gridView != null) gridView.ViewChanged -= RenderDebugPins;
#endif
            if (!sceneUIRoot.TryGetElement<FormationGridView>(FormationUIElementIds.GridRoot, out gridView))
            {
                WarnMissing(FormationUIElementIds.GridRoot);
                return false;
            }

            if (!sceneUIRoot.TryGetElement<FormationInfoPanelView>(FormationUIElementIds.InfoPanelRoot, out infoPanelView))
            {
                WarnMissing(FormationUIElementIds.InfoPanelRoot);
                return false;
            }

#if UNITY_EDITOR
            // 디버그 패널은 보조 기능이라 없어도 나머지 배치 UI는 정상 동작해야 한다 - 없으면 조용히 건너뛴다.
            sceneUIRoot.TryGetElement<FormationGridDebugView>(FormationUIElementIds.DebugPanelRoot, out debugView);
            // 핀 표시는 칸 좌표로 놓이므로 확대/축소로 칸 좌표가 바뀔 때마다 다시 놓는다(2026-09-29 실전 확인 - 줌 후 핀이 어긋남).
            gridView.ViewChanged += RenderDebugPins;
#endif

            var rootCanvas = panelRoot.GetComponentInParent<Canvas>()?.rootCanvas;
            var grid = gridView;
            dragCoordinator.Rebind(dragGhostPrefab, rootCanvas, () => grid.IconWorldSize);

            panelRoot.SetActive(false);
            return true;
        }

        public void SetAreaOutline(IFormationAreaOutline outline) => areaOutline = outline;

        // 칸별 배경 강조(설계 79번 §8 정리 모드)를 격자에 넘긴다. 격자가 사전을 칸 번호 기준으로 들고 있다가 전체 다시 그리기(SetView)로 슬롯이
        // 재배정될 때마다 다시 칠하므로, 편집기는 따로 다시 적용하지 않는다. null = 해제.
        public void SetCellTints(IReadOnlyDictionary<int, Color> tints) => gridView?.SetCellTints(tints);

        public void SetSources(ICaravanRosterProvider rosterProvider, IUnitConditionRepository conditionRepository)
        {
            this.rosterProvider = rosterProvider;
            this.conditionRepository = conditionRepository;
        }

        public void Open()
        {
            if (panelRoot == null)
            {
                return;
            }

            RefreshRosterCache();
            displayLayout = handler.GetDisplayLayout();
            gridView.Initialize(HandleSlotDropped, HandleUnitIconClicked, HandleGridIconBeginDrag, HandleIconDrag, HandleIconEndDrag);
#if UNITY_EDITOR
            // 디버그 패널은 게임 UI 인스톨러가 늘 만들지만, 핀 저장소(Bootstrap, Install/Remove 메뉴)가 없으면 쓸 수 없으므로 숨긴다.
            if (debugView != null)
            {
                debugView.gameObject.SetActive(handler.HasDebugPinStore);
                if (handler.HasDebugPinStore) debugView.Initialize(HandleDebugPinDropped, HandleDebugPinClicked);
            }
#endif

            // 표시 범위 전체가 들어오는 배율은 뷰포트 크기로 계산하므로, 패널을 먼저 켜고 레이아웃을 확정한 뒤 그린다.
            panelRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            lastWagonCount = -1;
            RefreshAllSlots();
            infoPanelView.Clear();
        }

        /// <summary>
        /// 씬 로딩 중(검은 커튼 뒤) 칸·아이콘 풀을 미리 채운다(Docs/설계/67번 §4.4) - 칸 오브젝트를 처음 열 때 한 번에 만들어 첫 클릭이
        /// 멈추던 것을 로딩 시간으로 옮긴다. 표시 범위 계산에 뷰포트 크기가 필요해 여는 경로를 그대로 한 번 지나고 곧바로 닫는다.
        /// 같은 프레임 안에서 끝나고 커튼(정렬 순서 100)이 덮고 있어 보이지 않는다.
        /// </summary>
        public void Prewarm()
        {
            if (panelRoot == null) return;

            Open();
            Close();
        }

        public void Close()
        {
            if (panelRoot == null)
            {
                return;
            }

            // 드래그 도중 패널이 강제로 닫히면(예: Field 씬에서 인카운터 발생 시) dragGhost가
            // panelRoot가 아니라 rootCanvas 바로 아래 별도로 떠 있어 panelRoot 비활성화와 무관하게
            // 화면에 그대로 남는다 - 패널을 닫을 때는 항상 진행 중인 드래그도 함께 정리한다.
            dragCoordinator.CancelActiveDrag();
            panelRoot.SetActive(false);
        }

        // 지금 도착 고스트를 드래그 중인 유닛의 활동이 백그라운드에서 완료/취소됐다면(드래그를 놓지
        // 않은 채 목적지에 도착하는 경우 등) 드래그 상태를 함께 정리한다 - 오버레이 자체는 다음
        // RequestRefresh에서 사라지지만, 드래그 고스트는 별도 오브젝트라 그것만으로는 지워지지 않고
        // 마우스 커서를 계속 따라다닌다(실전 확인, 2026-09-07). 해당 유닛을 드래그하는 중이 아니면
        // 아무 일도 하지 않는다.
        public void CancelDragIfRedirectingUnit(string unitId)
        {
            if (dragCoordinator.IsRedirect && dragCoordinator.DraggedUnit?.Id == unitId)
            {
                dragCoordinator.CancelActiveDrag();
            }
        }

        // 핸들러가 백그라운드에서 상태를 바꿨을 때(Field 활동 완료/취소 등, Docs/설계/25번 §3.4) 다시
        // 그리라고 요청한다 - 패널이 닫혀 있으면 아무 것도 하지 않는다(다시 열 때 Open()이 최신
        // 상태를 자연히 반영한다).
        public void RequestRefresh()
        {
            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            RefreshRosterCache();
            displayLayout = handler.GetDisplayLayout();
            RefreshAllSlots();
        }

        // 진행 중인 배치/이동 활동을 경로선 + 슬롯 오버레이 마크 + 이동 아이콘으로 그린다(설계 25번
        // §5) - 잔여시간이 매 프레임 줄어들므로 FieldFormationPanel.Update()가 매 프레임 호출한다.
        // Hub는 GetActiveActivities()가 항상 빈 목록이라 이 메서드 전체가 자연히 무해하다. 세 호출
        // (SetPathLines→SetActivityOverlays→SetTravelerIcons) 순서가 곧 렌더 순서다(실전 확인:
        // 슬롯 배경 < 경로선 < 오버레이 마크 < 이동 아이콘) - 순서를 바꾸지 말 것.
        public void TickActivityOverlays()
        {
            if (panelRoot == null || !panelRoot.activeSelf || displayLayout == null)
            {
                return;
            }

            var activities = activityHandler?.GetActiveActivities() ?? Array.Empty<FormationActivity>();
            activityOverlayBuffer.Clear();
            movePathBuffer.Clear();

            foreach (var activity in activities)
            {
                unitsById.TryGetValue(activity.UnitId, out var unit);

                activityOverlayBuffer.Add(new FormationActivityOverlayVisual(activity.TargetSlotIndex, activity, unit));

                if (activity.Kind == FormationActivityKind.Moving)
                {
                    activityOverlayBuffer.Add(new FormationActivityOverlayVisual(activity.OriginSlotIndex, activity, unit));
                    // 이동 중인 유닛 아이콘이 경로 위를 실시간으로 지나가도록(기획 20번 §3.3) 진행률과
                    // 아이콘을 함께 넘긴다 - 정적인 출발/도착 오버레이(위 activityOverlayBuffer)와는 별개다.
                    movePathBuffer.Add(new FormationMovePathVisual(activity.PathSlotIndices, activity.Progress01, unit?.Icon, activity.PartialSegmentIndex, activity.PartialSegmentWeight));
                }
            }

            gridView.SetPathLines(movePathBuffer);
            gridView.SetActivityOverlays(activityOverlayBuffer, HandleActivityGhostBeginDrag, HandleIconDrag, HandleActivityGhostEndDrag);
            gridView.SetTravelerIcons(movePathBuffer);
        }

        private void RefreshRosterCache()
        {
            currentRoster = rosterProvider?.GetRoster() ?? Array.Empty<IFormationUnit>();
            unitsById.Clear();
            foreach (var unit in currentRoster)
            {
                unitsById[unit.Id] = unit;
            }
        }

        private void HandleUnitIconClicked(IFormationUnit unit) => infoPanelView.Show(unit);

        // 카테고리 행 클릭 시 같은 카테고리 개체는 전부 스탯/외형이 동일하므로(기획 11번 §3), 대표로
        // 로스터에서 그 카테고리의 첫 개체 정보를 보여준다.
        private void HandlePaletteRowClicked(FormationCategoryKey key)
        {
            foreach (var unit in currentRoster)
            {
                if (FormationCategoryKey.Of(unit).Equals(key))
                {
                    infoPanelView.Show(unit);
                    return;
                }
            }
        }

        // 카테고리 행은 구체적인 개체를 모른다(설계 16번) - 여기서 그 카테고리의 가용(미배치+생존+
        // 미예약) 개체 하나를 골라 드래그 코디네이터에 그대로 넘긴다.
        private void HandlePaletteRowBeginDrag(FormationCategoryKey key, PointerEventData eventData)
        {
            var available = FindAvailableUnit(key);
            if (available == null)
            {
                return; // 방어적 - 잔여 0이면 행 자체가 비활성화라 정상 흐름에선 호출되지 않는다.
            }

            dragCoordinator.BeginFromPalette(available, eventData);
        }

        private IFormationUnit FindAvailableUnit(FormationCategoryKey key)
        {
            var placedIds = CollectPlacedIds();
            foreach (var unit in currentRoster)
            {
                if (!FormationCategoryKey.Of(unit).Equals(key)) continue;
                if (placedIds.Contains(unit.Id)) continue;
                if (activityHandler?.IsUnitReserved(unit.Id) ?? false) continue;
                if (conditionRepository != null && unit is IMercenaryUnit && conditionRepository.IsDead(unit.Id)) continue;

                return unit;
            }
            return null;
        }

        private HashSet<string> CollectPlacedIds()
        {
            var placedIds = new HashSet<string>();
            for (var i = 0; i < displayLayout.SlotCount; i++)
            {
                var id = displayLayout.GetUnitId(i);
                if (!string.IsNullOrEmpty(id))
                {
                    placedIds.Add(id);
                }
            }
            return placedIds;
        }

        private List<FormationCategorySummary> BuildCategorySummaries()
        {
            var placedIds = CollectPlacedIds();
            var order = new List<FormationCategoryKey>();
            var totals = new Dictionary<FormationCategoryKey, int>();
            var availables = new Dictionary<FormationCategoryKey, int>();
            var names = new Dictionary<FormationCategoryKey, string>();
            var icons = new Dictionary<FormationCategoryKey, Sprite>();

            foreach (var unit in currentRoster)
            {
                if (!handler.ShowsInPalette(unit)) continue;

                var key = FormationCategoryKey.Of(unit);
                if (!totals.ContainsKey(key))
                {
                    order.Add(key);
                    totals[key] = 0;
                    availables[key] = 0;
                    names[key] = unit.DisplayName;
                    icons[key] = unit.Icon;
                }

                totals[key]++;

                var isDead = conditionRepository != null && unit is IMercenaryUnit && conditionRepository.IsDead(unit.Id);
                // Field에서는 아직 배치가 완료되지 않았어도(레이아웃엔 안 나타남) 이미 다른 진행 중
                // 활동에 쓰이고 있으면 예약된 것으로 본다(activityHandler.IsUnitReserved, 설계 25번 §3.3).
                var isReserved = placedIds.Contains(unit.Id) || (activityHandler?.IsUnitReserved(unit.Id) ?? false);
                if (!isDead && !isReserved)
                {
                    availables[key]++;
                }
            }

            var summaries = new List<FormationCategorySummary>(order.Count);
            foreach (var key in order)
            {
                summaries.Add(new FormationCategorySummary(key, names[key], icons[key], totals[key], availables[key]));
            }
            return summaries;
        }

        private void RefreshPalette()
        {
            paletteView.SetCategories(BuildCategorySummaries(), HandlePaletteRowClicked, HandlePaletteRowBeginDrag, HandleIconDrag, HandleIconEndDrag);
        }

        private void HandleGridIconBeginDrag(int originSlotIndex, FormationUnitIconView icon, PointerEventData eventData)
        {
            var unitId = displayLayout.GetUnitId(originSlotIndex);
            if (string.IsNullOrEmpty(unitId) || !unitsById.TryGetValue(unitId, out var unit))
            {
                return;
            }

            // 이동 중인 유닛은 완료 전까지 FormationLayout 상 출발 슬롯을 계속 점유한 것으로 기록돼
            // (설계 25번 §3.2) 그 슬롯의 아이콘이 평범한 배치 아이콘처럼 집힌다. 이 경우 일반 그리드
            // 이동으로 처리하면 활동이 취소+재시작돼 출발지로 순간이동한 뒤 처음부터 다시 이동하는
            // 것처럼 보이는 버그가 있었다(실전 확인, 2026-09-07) - 도착 고스트를 드래그한 것과 동일한
            // 목적지 재조정으로 처리한다.
            if (IsMovingAwayFrom(originSlotIndex, unitId))
            {
                dragCoordinator.BeginRedirectFromGrid(unit, originSlotIndex, eventData);
                return;
            }

            dragCoordinator.BeginFromGrid(unit, originSlotIndex, eventData);
        }

        private bool IsMovingAwayFrom(int slotIndex, string unitId)
        {
            var activities = activityHandler?.GetActiveActivities() ?? Array.Empty<FormationActivity>();
            foreach (var activity in activities)
            {
                if (activity.UnitId == unitId && activity.Kind == FormationActivityKind.Moving && activity.OriginSlotIndex == slotIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private void HandleIconDrag(PointerEventData eventData) => dragCoordinator.UpdateGhostPosition(eventData);

        // 도착 고스트 드래그 시작(기획 21번, 설계 26번 §5.5) - unitId는 FormationActivityOverlayView가
        // 이미 리다이렉트 가능한 마크일 때만 넘겨준다(FormationActivityOverlayVisual.IsRedirectableTarget).
        private void HandleActivityGhostBeginDrag(string unitId, PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(unitId) || !unitsById.TryGetValue(unitId, out var unit))
            {
                return;
            }

            dragCoordinator.BeginRedirect(unit, eventData);
        }

        // 슬롯에 드롭됐다면 HandleSlotDropped가 이미 activityHandler.HandleRedirectMove를 호출했다 - 여기서는
        // 드래그 상태만 정리한다. 빈 곳에 드롭해도(취소) 진행 중이던 이동 자체는 그대로 유지되므로
        // (유닛 아이콘 드래그의 "빈 곳=제거" 규칙과 다르다) 슬롯/팔레트를 다시 그릴 필요가 없다.
        private void HandleActivityGhostEndDrag(PointerEventData eventData) => dragCoordinator.EndDrag();

        private void HandleSlotDropped(int targetSlotIndex)
        {
            var draggedUnit = dragCoordinator.DraggedUnit;
            if (draggedUnit == null)
            {
                return;
            }

            dragCoordinator.MarkDropHandled();

            // 도착 고스트 드래그(기획 21번) - 픽업한 슬롯이 없어 아래 "그리드 슬롯→슬롯" 분기와
            // 겹치지 않으므로 가장 먼저 처리한다. 화면 갱신은 여기서 하지 않는다 - 다음 프레임
            // TickActivityOverlays()가 새 목적지 위치로 자연히 다시 그린다.
            if (dragCoordinator.IsRedirect)
            {
                // IsRedirect는 BeginRedirect/BeginRedirectFromGrid를 거쳐야만 true가 되고, 그 경로는
                // activityHandler가 있을 때만(진행 중 활동이 있을 때만) 도달하므로 여기서는 항상
                // non-null이다.
                activityHandler?.HandleRedirectMove(draggedUnit.Id, targetSlotIndex);
                return;
            }

            if (dragCoordinator.DraggedFromSlot is { } sourceIndex)
            {
                if (sourceIndex == targetSlotIndex)
                {
                    return;
                }
                // 이동이 거부되면(대열 밖·마차 연결 끊김) 옮기려던 유닛의 칸을 깜빡인다(설계 60번 §11-5).
                if (!handler.HandleGridMove(draggedUnit.Id, sourceIndex, targetSlotIndex)) gridView.FlashRejected(sourceIndex);
            }
            else
            {
                if (!handler.HandlePaletteDrop(draggedUnit, targetSlotIndex)) gridView.FlashRejected(targetSlotIndex);
            }

            // 화면 갱신은 여기서 하지 않는다 - 원본 슬롯 아이콘은 아직 드래그 중인 오브젝트라 지금 다시 그리면 뒤이은 OnEndDrag가
            // 씹힌다(기존 FormationPanel 동작). 대열 영역이 바뀌었을 수 있어 드래그가 끝나는 HandleIconEndDrag에서 전체를 다시 그린다.
        }

        private void HandleIconEndDrag(PointerEventData eventData)
        {
            // EndDrag()가 상태를 리셋하며 IsRedirect도 false로 되돌리므로 리셋 전에 먼저 읽어둔다.
            var wasRedirect = dragCoordinator.IsRedirect;
            var (unit, fromSlot, wasHandled) = dragCoordinator.EndDrag();

            // 출발 슬롯에서 집은 재조정 드래그(HandleGridIconBeginDrag의 예외처리)는 빈 곳에
            // 드롭해도(취소) 진행 중이던 이동을 제거하지 않는다 - 도착 고스트 드래그
            // (HandleActivityGhostEndDrag)와 동일한 규칙.
            if (!wasRedirect && !wasHandled && fromSlot.HasValue && unit != null)
            {
                // 타일/팔레트가 아닌 곳에 드롭 = 배치 취소/제거(기획 20번 §3.4). 마차 연결을 끊는 제거면 거부되고 칸이 깜빡인다.
                if (!handler.HandleRemove(unit.Id, fromSlot.Value)) gridView.FlashRejected(fromSlot.Value);
            }

            // 화면 갱신은 반드시 여기(드래그가 실제로 끝나는 시점)에서 한다 - OnDrop 시점(HandleSlotDropped)에는 원본 아이콘이
            // 아직 드래그 중인 오브젝트라, 거기서 갱신하면 뒤이은 OnEndDrag 호출이 씹혀 드래그 상태가 초기화되지 않는 문제가 있었다.
            // 배치·이동·제거로 대열 영역이 바뀌었을 수 있어 표시 범위부터 전체를 다시 그린다.
            displayLayout = handler.GetDisplayLayout();
            RefreshAllSlots();
        }

        private void RefreshSlot(int index)
        {
            var unitId = displayLayout.GetUnitId(index);
            IFormationUnit unit = null;
            if (!string.IsNullOrEmpty(unitId))
            {
                unitsById.TryGetValue(unitId, out unit);
            }

            gridView.RenderSlot(index, unit);
        }

        // 대열 영역을 다시 계산해 표시 범위(경계 상자 + 여백, 판 경계로 자름)와 대열 칸 표시를 갱신하고, 보이는 칸만 다시 그린다.
        private const int FitMarginCells = 2;

        // 경계 상자를 여백만큼 넓히고 판 경계로 자른다.
        private RectInt ExpandBounds(RectInt bounds, int margin)
        {
            var xMin = Mathf.Max(0, bounds.xMin - margin);
            var yMin = Mathf.Max(0, bounds.yMin - margin);
            var xMax = Mathf.Min(displayLayout.ColumnCount, bounds.xMax + margin);
            var yMax = Mathf.Min(displayLayout.RowCount, bounds.yMax + margin);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private void RefreshAllSlots()
        {
            var area = FormationArea.Compute(displayLayout, LookupUnit, handler.GetAreaPins());
            // 최대 축소 범위(대열 + 2칸)와 칸을 까는 범위(대열 + 정책 여백 - 마을은 연결 가능한 칸까지)를 나눈다. 둘을 같게 두면 마을의 넓은 여백 때문에
            // 최대 축소 화면에서 대열이 너무 작아진다(2026-09-29 사용자 결정 - 축소 한계만 2칸, 먼 칸은 스크롤로 접근).
            var fitRange = ExpandBounds(area.Bounds, FitMarginCells);
            var contentRange = ExpandBounds(area.Bounds, Mathf.Max(FitMarginCells, handler.GetVisibleMarginCells()));
            // 열 때는 늘 "범위 전체 보기"로 맞춘다(기획 59번 §4.4 - 이전에 열었을 때의 배율이 남으면 여는 화면이 최대 축소가 아니게 된다, 2026-09-29
            // V-02 실전 확인). 마차가 없으면 전체 보기의 중앙이 곧 기준 칸이다. 편집 중 마차가 0대가 되는 순간에는 배율은 두고 기준 칸(원점)으로만
            // 옮긴다(2026-09-29 사용자 요청) - 마차가 없는 동안 매번 옮기면 디버그 핀 편집 중에도 화면이 튄다.
            var justOpened = lastWagonCount < 0;
            Vector2? focus = null;
            if (!justOpened && area.WagonCount == 0 && lastWagonCount > 0)
            {
                focus = new Vector2(area.AnchorSlotIndex % area.ColumnCount + 0.5f, area.AnchorSlotIndex / area.ColumnCount + 0.5f);
            }
            lastWagonCount = area.WagonCount;
            gridView.SetView(displayLayout.ColumnCount, displayLayout.RowCount, fitRange, contentRange, area.Contains, focus, fitView: justOpened);

            foreach (var index in gridView.VisibleSlotIndices)
            {
                RefreshSlot(index);
            }
#if UNITY_EDITOR
            RenderDebugPins();
#endif
            areaOutline?.Render(area, gridView);
            // 슬롯 배치가 통째로 바뀌었을 수 있으므로 팔레트 잔여수도 다시 계산한다 - 카테고리는 최대 5개뿐이라 매번 다시 그려도
            // 비용이 미미하다(설계 16번 §3).
            RefreshPalette();
        }

#if UNITY_EDITOR
        private void RenderDebugPins()
        {
            if (debugView == null || !debugView.isActiveAndEnabled || handler == null) return;
            debugView.RenderPins(handler.GetAreaPins(), gridView.ContentRect, gridView.IsVisible, gridView.GetSlotAnchoredPosition, gridView.CellSize);
        }

        private void HandleDebugPinDropped(int slotIndex, FormationAreaShape shape)
        {
            handler.HandleDebugPinAdd(slotIndex, shape);
            displayLayout = handler.GetDisplayLayout();
            RefreshAllSlots();
        }

        // 핀 제거가 마차 연결을 끊으면 거부되고 그 칸이 깜빡인다(기획 59번 §4.5).
        private void HandleDebugPinClicked(int slotIndex)
        {
            if (!handler.HandleDebugPinRemove(slotIndex)) gridView.FlashRejected(slotIndex);
            displayLayout = handler.GetDisplayLayout();
            RefreshAllSlots();
        }
#endif

        private IFormationUnit LookupUnit(string unitId)
            => !string.IsNullOrEmpty(unitId) && unitsById.TryGetValue(unitId, out var unit) ? unit : null;

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"Formation UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
        }
    }
}
