using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Field(상행 중 이동뷰/전투뷰)에서 쓰는 배치(Formation) UI 조율자 - Hub와 달리 Apply 버튼 없이
    /// 모든 행동이 즉시 반영된다(Docs/기획/20번 §3.1). 실제 반영은 IFormationRepository에 곧바로
    /// 쓰지 않고 IFieldFormationActivityRepository에 배치/이동 활동을 등록하는 것으로 대신한다 -
    /// 그 저장소가 시간이 지나 활동을 완료 처리할 때 비로소 FormationLayout에 반영된다(설계 25번
    /// §3.2). 그리드/팔레트 렌더링과 드래그 이벤트 중계는 공용 로직(FormationGridEditor)에 위임한다.
    /// 마차 중심 대열 규칙(Docs/기획/59번, 설계 60번 §5): 대열 영역은 배치 완료된 유닛만으로 계산한다(진행 중 배치는 영역을 넓히지 않음).
    /// 배치·이동 목표는 현재 영역 안이어야 하고, 이동·제거는 편집 후 배치로 마차 연결을 검사한다. 영역이 줄면 영역 밖 유닛을 자동 해제하고
    /// 목표가 영역 밖이 된 진행 중 활동을 취소한다. 이동 경로는 영역 밖 칸을 지나지 않는다.
    /// </summary>
    public class FieldFormationPanel : MonoBehaviour, IUIPanel, IFormationEditingHandler, IFormationActivityHandler
    {
        [SerializeField] private FormationUnitIconView dragGhostPrefab;

        public string PanelId => UIPanelIds.Formation;

        private FormationGridEditor gridEditor;
        private Button closeButton;

        private ICaravanRosterProvider rosterProvider;
        private IFormationRepository formationRepository;
        private IFieldFormationActivityRepository activityRepository;
        private IUIManager uiManager;
        // 디버그 핀(에디터 전용 도구, 같은 UIManager 오브젝트에 설치된 경우만) - 없으면 null.
        private IFormationDebugAreaSource debugAreaSource;

        public void RegisterFieldFormationUI(SceneUIRoot sceneUIRoot, ICaravanRosterProvider rosterProvider, IFormationRepository formationRepository, IUnitConditionRepository conditionRepository, IFieldFormationActivityRepository activityRepository, IUIManager uiManager)
        {
            this.rosterProvider = rosterProvider;
            this.formationRepository = formationRepository;
            this.activityRepository = activityRepository;
            this.uiManager = uiManager;
            debugAreaSource = GetComponent<IFormationDebugAreaSource>();

            gridEditor = new FormationGridEditor(this);
            if (!gridEditor.TryBind(sceneUIRoot, dragGhostPrefab))
            {
                return;
            }

            // Field 배치 UI에는 Apply 버튼이 없다(즉시 반영) - Close 버튼만 찾는다.
            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.CloseButton, out closeButton))
            {
                Debug.LogWarning($"Formation UI에서 '{FormationUIElementIds.CloseButton}' 요소를 찾을 수 없다.");
                return;
            }

            gridEditor.SetSources(rosterProvider, conditionRepository);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => uiManager.Close(PanelId));

            // 백그라운드에서 활동이 완료/취소될 때마다(패널이 열려 있는 동안) 화면을 다시 그린다.
            if (activityRepository != null)
            {
                activityRepository.OnActivityCompleted -= HandleActivityChanged;
                activityRepository.OnActivityCompleted += HandleActivityChanged;
                activityRepository.OnActivityCancelled -= HandleActivityCancelled;
                activityRepository.OnActivityCancelled += HandleActivityCancelled;
            }
        }

        public void Open() => gridEditor.Open();
        public void Close() => gridEditor?.Close();

        // 진행 중인 활동은 매 프레임 잔여시간이 줄어들므로(설계 25번 §5.1), 패널이 열려 있는 동안
        // 매 프레임 오버레이/경로선을 다시 그린다 - RequestRefresh(이벤트 기반)와 별개 경로다.
        private void Update() => gridEditor?.TickActivityOverlays();

        public IReadOnlyList<FormationActivity> GetActiveActivities() => activityRepository?.ActiveActivities ?? Array.Empty<FormationActivity>();

        // 완료/취소된 활동의 도착 고스트를 지금 드래그하는 중이었다면(예: 드래그를 놓지 않은 채
        // 목적지에 도착) 드래그 상태를 함께 정리한다 - 안 그러면 오버레이 자체는 사라져도 별도
        // 오브젝트인 드래그 고스트만 화면에 남아 마우스 커서를 계속 따라다닌다(실전 확인, 2026-09-07).
        private void HandleActivityChanged(FormationActivity activity)
        {
            // 이동 완료로 마차·시설 위치가 바뀌면 영역이 달라질 수 있다 - 영역 밖 유닛·활동을 정리한다.
            NormalizeToArea();
            gridEditor?.CancelDragIfRedirectingUnit(activity.UnitId);
            gridEditor?.RequestRefresh();
        }

        private void HandleActivityCancelled(string unitId)
        {
            gridEditor?.CancelDragIfRedirectingUnit(unitId);
            gridEditor?.RequestRefresh();
        }

        // IFormationEditingHandler 구현 - 전부 실시간 반영(로컬 사본 없음).
        public FormationLayout GetDisplayLayout()
        {
            if (formationRepository != null && formationRepository.TryLoadCurrent(out var layout))
            {
                return layout;
            }
            return FormationLayout.CreateDefault();
        }

        public IReadOnlyList<FormationAreaPin> GetAreaPins() => debugAreaSource?.Pins;

        // 상행 중은 대열 안에만 놓으므로 여백 2칸(설계 60번 §15.3 - 마차 자유 배치는 마을만).
        public int GetVisibleMarginCells() => 2;

        // 상행 중 팔레트에는 마차를 보이지 않는다(새로 놓을 수 없음, 2026-09-29 사용자 결정).
        public bool ShowsInPalette(IFormationUnit unit) => unit.Kind != FormationUnitKind.Wagon;

#if UNITY_EDITOR
        public bool HasDebugPinStore => debugAreaSource != null;

        public void HandleDebugPinAdd(int slotIndex, FormationAreaShape shape) => debugAreaSource?.AddOrReplace(new FormationAreaPin(slotIndex, shape));

        // 핀 제거로 영역이 줄면 영역 밖 유닛 해제·활동 취소까지 마차 제거와 같게 처리한다.
        public bool HandleDebugPinRemove(int slotIndex)
        {
            if (debugAreaSource == null) return false;

            var layout = GetDisplayLayout();
            var remaining = new List<FormationAreaPin>(debugAreaSource.Pins);
            remaining.RemoveAll(p => p.SlotIndex == slotIndex);
            var result = FormationAreaRules.Validate(layout.Clone(), FormationAreaRules.LookupFrom(rosterProvider), remaining);
            if (!result.Accepted) return false;

            debugAreaSource.Remove(slotIndex);
            if (formationRepository != null) ApplyAndCancelOutside(result);
            return true;
        }
#endif

        public bool IsUnitReserved(string unitId) => activityRepository != null && activityRepository.IsUnitBusy(unitId);

        public bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex)
        {
            if (activityRepository == null) return false;
            // 상행 중에는 마차를 새로 놓거나 뺄 수 없다(2026-09-29 사용자 결정 - 이동만 허용). 팔레트에서도 숨기지만 방어적으로 한 번 더 막는다.
            if (unit.Kind == FormationUnitKind.Wagon) return false;

            // 목표는 현재 대열 칸이어야 하고(마차가 없으면 기준 칸엔 마차만), 상행 중엔 비어 있는 칸에만 놓는다 - 배치 완료 시점에
            // 점유 유닛을 덮어쓰면 그 유닛이 마차일 때 연결 판정 없이 빠지기 때문이다.
            var layout = GetDisplayLayout();
            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            if (FormationAreaRules.CanPlaceAt(FormationArea.Compute(layout, lookup, GetAreaPins()), unit, targetSlotIndex) != FormationEditRejection.None) return false;
            if (!string.IsNullOrEmpty(GetOccupantTreatingMovedOriginAsEmpty(layout, targetSlotIndex)) || IsActivityTarget(targetSlotIndex)) return false;

            var duration = unit.Kind switch
            {
                FormationUnitKind.Wagon => FormationTiming.WagonAddSeconds,
                FormationUnitKind.Facility => FormationTiming.FacilityAddSeconds,
                _ => FormationTiming.CharacterAddSeconds,
            };
            activityRepository.BeginAdd(unit.Id, targetSlotIndex, duration);
            return true;
        }

        public bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex)
        {
            if (activityRepository == null || formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return false;

            // 이동을 끝낸 뒤의 배치(맞바꾸기 포함)로 대열 규칙을 먼저 검사한다 - 활동을 취소하기 전에 판정해야 거부 시 원래 상태가 유지된다.
            if (!FormationAreaRules.Move(layout, originSlotIndex, targetSlotIndex, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins()).Accepted) return false;

            // 이미 이동 중인 유닛을 다시 드래그하면(재조정) 기존 활동을 취소하고 새로 시작한다 -
            // 그대로 두면 같은 유닛에 활동이 두 개 겹쳐 상태가 어긋난다(설계 25번 §3.2 안전장치).
            if (activityRepository.IsUnitBusy(unitId))
            {
                activityRepository.Cancel(unitId);
            }

            // 목표 슬롯에 이미 다른 유닛이 있으면 그 유닛도 반대 방향(내 출발 슬롯)으로 맞바꾸어
            // 이동시킨다 - 그냥 덮어쓰면 원래 있던 유닛이 레이아웃에서 통째로 사라지던 버그(실전
            // 검증 2026-09-07)의 정정. 그 유닛이 이미 이동 중이었다면 먼저 취소하고 새로 시작한다.
            var targetUnitId = GetOccupantTreatingMovedOriginAsEmpty(layout, targetSlotIndex);
            if (!string.IsNullOrEmpty(targetUnitId) && targetUnitId != unitId)
            {
                if (activityRepository.IsUnitBusy(targetUnitId))
                {
                    activityRepository.Cancel(targetUnitId);
                }
                StartMove(layout, targetUnitId, targetSlotIndex, originSlotIndex);
            }

            StartMove(layout, unitId, originSlotIndex, targetSlotIndex);
            return true;
        }

        private void StartMove(FormationLayout layout, string unitId, int originSlotIndex, int targetSlotIndex)
        {
            var blocked = CollectBlockedSlots(layout, excludeUnitId: unitId);
            var path = FormationPathFinder.FindPath(originSlotIndex, targetSlotIndex, layout.ColumnCount, layout.RowCount, blocked);
            // 대각선 구간(√2배, 기획 21번, 설계 26번 §10)이 섞일 수 있어 칸 수가 아니라 실제 비용
            // 합계로 소요시간을 계산한다.
            var duration = Mathf.Max(FormationTiming.MoveSecondsPerSlot, FormationPathFinder.TotalCost(path, layout.ColumnCount) * FormationTiming.MoveSecondsPerSlot);
            activityRepository.BeginMove(unitId, originSlotIndex, targetSlotIndex, path, duration);
        }

        // 도착 고스트를 드래그해 목적지를 바꾼다(기획 21번, 설계 26번 §4/§2). "현재 위치"는 그리드에
        // 정확히 맞지 않는 연속 좌표일 수 있다 - 다음 노드로 스냅하던 최초 구현은 리다이렉트 순간
        // 캐릭터가 뚝뚝 끊겨 순간이동하는 것처럼 보인다는 문제가 있어(실전 확인, 2026-09-07),
        // FormationPathInterpolation의 "부분 구간" 가중치 지원으로 정확한 연속 위치에서 그대로
        // 이어지도록 정정했다. 출발지→현재 위치 구간은 저장된 옛 경로 배열을 잘라 쓰지 않고 매번
        // 새로 BFS 계산한다 - 저장된 배열을 재사용하면 리다이렉트를 반복할수록 이미 무의미해진 과거
        // 목적지들의 우회 구간까지 계속 누적되어 그려지는 문제가 있었다(실전 확인, 2026-09-07).
        public void HandleRedirectMove(string unitId, int newTargetSlotIndex)
        {
            if (activityRepository == null || formationRepository == null) return;
            if (!activityRepository.TryGetActivity(unitId, out var activity) || activity.Kind != FormationActivityKind.Moving) return;
            if (!formationRepository.TryLoadCurrent(out var layout)) return;
            // 새 목적지도 대열 안이어야 한다(시설은 마차·핀 영역 안).
            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            if (!FormationArea.Compute(layout, lookup, GetAreaPins()).CanHost(lookup(unitId), newTargetSlotIndex)) return;

            // 지금 정확히 어느 구간(fromNode→toNode)의 몇 %(localT) 지점에 있는지 찾는다 - 이 활동
            // 자신이 이전 리다이렉트로 이미 부분 구간을 갖고 있어도(PartialSegmentIndex/Weight)
            // 똑같은 계산으로 정확히 반영된다(설계 26번 §2.1). 대각선 구간(√2배)이 섞여 있을 수 있어
            // 구간별 실제 비용 배열을 먼저 구해서 넘긴다(설계 26번 §10).
            var currentWeights = FormationPathFinder.ComputeSegmentWeights(activity.PathSlotIndices, layout.ColumnCount, activity.PartialSegmentIndex, activity.PartialSegmentWeight);
            var (segmentIndex, localT) = FormationPathInterpolation.Locate(activity.PathSlotIndices.Count, activity.Progress01, currentWeights);
            var fromNode = activity.PathSlotIndices[segmentIndex];
            var toNode = activity.PathSlotIndices[segmentIndex + 1];

            if (toNode == newTargetSlotIndex) return; // 이미 그 지점으로 향하는 중 - 손댈 것 없음

            // 목적지 점유 시 맞바꾸기(기획 21번 §3.2, 버그#8과 동일 규칙 - 일반 재조정 맞바꾸기와
            // 똑같이 "상대는 이 유닛의 원래 출발지로 향한다"로 통일한다).
            var targetUnitId = GetOccupantTreatingMovedOriginAsEmpty(layout, newTargetSlotIndex);
            if (!string.IsNullOrEmpty(targetUnitId) && targetUnitId != unitId)
            {
                if (activityRepository.IsUnitBusy(targetUnitId))
                {
                    activityRepository.Cancel(targetUnitId);
                }
                StartMove(layout, targetUnitId, newTargetSlotIndex, activity.OriginSlotIndex);
            }

            var blocked = CollectBlockedSlots(layout, excludeUnitId: unitId);
            var prefix = FormationPathFinder.FindPath(activity.OriginSlotIndex, fromNode, layout.ColumnCount, layout.RowCount, blocked);
            var prefixCost = FormationPathFinder.TotalCost(prefix, layout.ColumnCount);
            var currentSegmentBaseCost = FormationPathFinder.SegmentCost(fromNode, toNode, layout.ColumnCount); // 대각선이면 √2

            // 지금 진행 방향 그대로 toNode까지 마저 간 뒤 새 목적지로(전진) vs 즉시 fromNode로
            // 되돌아가 새 목적지로(반전) - 둘 다 계산해 총 남은 비용이 더 짧은 쪽을 쓴다(사용자 확정,
            // 2026-09-07). 그냥 항상 전진만 고르면, 반대 방향으로 목적지를 바꿨을 때 원래 가던
            // 방향으로 한 칸 더 갔다가 되돌아오는 부자연스러운 움직임이 생겼다(실전 확인). 대각선
            // 구간(√2배, 설계 26번 §10)이 섞일 수 있어 칸 수가 아니라 실제 비용으로 비교한다.
            var suffixForward = FormationPathFinder.FindPath(toNode, newTargetSlotIndex, layout.ColumnCount, layout.RowCount, blocked);
            var suffixBackward = FormationPathFinder.FindPath(fromNode, newTargetSlotIndex, layout.ColumnCount, layout.RowCount, blocked);
            var suffixForwardCost = FormationPathFinder.TotalCost(suffixForward, layout.ColumnCount);
            var suffixBackwardCost = FormationPathFinder.TotalCost(suffixBackward, layout.ColumnCount);
            var forwardRemaining = currentSegmentBaseCost * (1f - localT) + suffixForwardCost;
            var backwardRemaining = currentSegmentBaseCost * localT + suffixBackwardCost;

            List<int> combinedPath;
            int partialSegmentIndex;
            float partialSegmentWeight;
            float requiredSeconds;
            if (forwardRemaining <= backwardRemaining)
            {
                // 전진 - fromNode→toNode 구간이 combinedPath 상에서 차지하는 인덱스는 prefix 바로
                // 다음(prefix.Count-1번째 구간)이다. 이미 localT만큼 지나왔으니 남은 가중치(기본
                // 비용 대비 비율)는 (1-localT)뿐이다 - 정확한 연속 위치에서 그대로 이어진다(끊김 없음).
                combinedPath = prefix.Concat(new[] { toNode }).Concat(suffixForward.Skip(1)).ToList();
                partialSegmentIndex = prefix.Count - 1;
                partialSegmentWeight = 1f - localT;
                requiredSeconds = (prefixCost + forwardRemaining) * FormationTiming.MoveSecondsPerSlot;
            }
            else
            {
                // 반전 - "지금 위치→fromNode"라는 중간 지점을 그리드 슬롯 배열로 정확히 표현할 방법이
                // 없어(연속 좌표가 fromNode/toNode 축 위에만 있고 다른 어떤 실제 노드와도 이어지지
                // 않음) 이 경우만 fromNode로 스냅하는 근사를 쓴다 - 그래도 기존 "무조건 toNode까지
                // 전진 후 반전"보다는 훨씬 짧은 움직임이라 체감상 자연스럽다.
                combinedPath = prefix.Concat(suffixBackward.Skip(1)).ToList();
                partialSegmentIndex = -1;
                partialSegmentWeight = 1f;
                requiredSeconds = Mathf.Max(FormationTiming.MoveSecondsPerSlot, (prefixCost + suffixBackwardCost) * FormationTiming.MoveSecondsPerSlot);
            }

            var elapsedSeconds = prefixCost * FormationTiming.MoveSecondsPerSlot;

            activityRepository.RedirectMove(unitId, newTargetSlotIndex, combinedPath, requiredSeconds, elapsedSeconds, partialSegmentIndex, partialSegmentWeight);
        }

        public bool HandleRemove(string unitId, int slotIndex)
        {
            // 상행 중 마차 제거 금지(2026-09-29 사용자 결정) - 이전의 "마차 최소 1대 유지"(기획 20번 §3.4)는 이 규칙에 포함돼 따로 두지 않는다.
            var rosterUnit = rosterProvider?.GetRoster().FirstOrDefault(u => u.Id == unitId);
            if (rosterUnit != null && rosterUnit.Kind == FormationUnitKind.Wagon) return false;

            if (activityRepository != null && activityRepository.IsUnitBusy(unitId))
            {
                // 진행 중(배치/이동) 제거 - 즉시 취소, 로스터 복귀(기획 20번 §3.4).
                activityRepository.Cancel(unitId);
                return true;
            }

            if (formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return false;

            var result = FormationAreaRules.Remove(layout, slotIndex, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins());
            if (!result.Accepted) return false;

            ApplyAndCancelOutside(result);
            return true;
        }

        // 영역 밖 유닛 자동 해제와 영역 밖 목표 활동 취소를 함께 반영한다(설계 60번 §5).
        private void ApplyAndCancelOutside(FormationEditResult result)
        {
            if (activityRepository != null)
            {
                foreach (var releasedId in result.ReleasedUnitIds)
                {
                    if (activityRepository.IsUnitBusy(releasedId)) activityRepository.Cancel(releasedId);
                }
            }

            formationRepository.Apply(result.Layout);

            if (activityRepository == null) return;
            var lookup = FormationAreaRules.LookupFrom(rosterProvider);
            var area = FormationArea.Compute(result.Layout, lookup, GetAreaPins());
            var outside = new List<string>();
            foreach (var activity in activityRepository.ActiveActivities)
            {
                if (!area.CanHost(lookup(activity.UnitId), activity.TargetSlotIndex)) outside.Add(activity.UnitId);
            }
            foreach (var outsideUnitId in outside) activityRepository.Cancel(outsideUnitId);
        }

        // 활동 완료 뒤 현재 배치를 대열 규칙에 맞춘다. 이동 시작 시점에 연결을 검사했지만, 그 사이 다른 편집이 끼면 도착 시점에 연결이
        // 끊길 수 있다 - 이미 끝난 이동은 되돌릴 수 없어 이 경우는 경고만 남기고 둔다.
        private void NormalizeToArea()
        {
            if (formationRepository == null || !formationRepository.TryLoadCurrent(out var layout)) return;

            var result = FormationAreaRules.Validate(layout.Clone(), FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins());
            if (!result.Accepted)
            {
                Debug.LogWarning($"{nameof(FieldFormationPanel)}: 이동 완료 후 마차 대열 연결이 끊겼다 - 마차 위치를 다시 조정하라.");
                return;
            }

            if (result.ReleasedUnitIds.Count > 0) ApplyAndCancelOutside(result);
        }

        private bool IsActivityTarget(int slotIndex)
        {
            if (activityRepository == null) return false;
            foreach (var activity in activityRepository.ActiveActivities)
            {
                if (activity.TargetSlotIndex == slotIndex) return true;
            }
            return false;
        }

        // 이동 중인 유닛은 도착 완료(Complete) 전까지 FormationLayout 상 여전히 출발 슬롯을 점유한
        // 것으로 기록된다(설계 25번 §3.2, InMemoryFieldFormationActivityRepository.ApplyToLayout의
        // "지금도 내가 차지하고 있을 때만 비운다" 판정이 성립하려면 필요) - 하지만 그 슬롯은 이미
        // 시각적으로 비어 있다(유닛이 이동 애니메이션 중). "목적지가 점유돼 있는지" 판정에 원본
        // GetUnitId를 그대로 쓰면, 이동 중인 유닛의 출발지로 다른 유닛을 드래그했을 때 점유로
        // 오인해 원래 이동이 순간이동하듯 취소되고 엉뚱한 맞바꾸기가 일어나는 버그가 있었다(실전
        // 확인, 2026-09-07). 그 슬롯이 어떤 유닛의 "지금 멀어지고 있는 출발지"와 정확히 일치할 때만
        // 빈 것으로 취급한다.
        private string GetOccupantTreatingMovedOriginAsEmpty(FormationLayout layout, int slotIndex)
        {
            var occupantId = layout.GetUnitId(slotIndex);
            if (string.IsNullOrEmpty(occupantId)) return occupantId;

            if (activityRepository != null && activityRepository.TryGetActivity(occupantId, out var occupantActivity)
                && occupantActivity.Kind == FormationActivityKind.Moving && occupantActivity.OriginSlotIndex == slotIndex)
            {
                return null;
            }

            return occupantId;
        }

        // 배치 그리드 위에서 점유된 슬롯 + 진행 중인 다른 활동의 목표 슬롯을 합쳐 "막힌 칸"으로
        // 본다(설계 25번 §4). excludeUnitId는 지금 이동을 시작하는 유닛 자신의 출발 슬롯을 막힌
        // 것으로 잘못 취급하지 않기 위한 제외 대상이다.
        private HashSet<int> CollectBlockedSlots(FormationLayout layout, string excludeUnitId)
        {
            var blocked = new HashSet<int>();

            // 대열 밖 칸은 지나갈 수 없다(설계 60번 §5).
            var area = FormationArea.Compute(layout, FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins());
            for (var i = 0; i < layout.SlotCount; i++)
            {
                if (!area.Contains(i)) blocked.Add(i);
            }

            for (var i = 0; i < layout.SlotCount; i++)
            {
                var id = layout.GetUnitId(i);
                if (!string.IsNullOrEmpty(id) && id != excludeUnitId)
                {
                    blocked.Add(i);
                }
            }

            if (activityRepository != null)
            {
                foreach (var activity in activityRepository.ActiveActivities)
                {
                    if (activity.UnitId != excludeUnitId)
                    {
                        blocked.Add(activity.TargetSlotIndex);
                    }
                }
            }

            return blocked;
        }
    }
}
