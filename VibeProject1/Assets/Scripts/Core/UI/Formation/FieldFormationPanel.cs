using System;
using System.Collections.Generic;
using TMPro;
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
    /// 편집 판정·반영은 편집 정책 객체(IFieldFormationEditPolicy)에 위임한다(설계 79번 §8) - 평소엔 활동 기반(FieldTimedEditPolicy), 전투 후 정리
    /// 모드(IFormationRepairMode)엔 즉시 반영(FieldRepairEditPolicy). 활동 완료 후 정규화·디버그 핀·활동 이벤트 중계는 두 정책이 공유하는 패널 몫으로 남긴다.
    /// </summary>
    public class FieldFormationPanel : MonoBehaviour, IUIPanel, IFormationEditingHandler, IFormationActivityHandler, ISceneLoadingTaskSource, IFormationRepairMode
    {
        private const string RepairGuideText = "대열이 끊어져 있습니다. 마차를 옮겨 대열을 이어 붙이세요.";

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

        private IFieldFormationEditPolicy timedPolicy;
        private IFieldFormationEditPolicy repairPolicy;
        private IFieldFormationEditPolicy currentPolicy;

        // 정리 모드 화면 요소(설계 79번 §8) - 없으면 정리 모드만 건너뛴다(평소 정비창은 정상 동작).
        private TMP_Text repairGuideLabel;
        private Button repairDoneButton;
        private Action repairCompleted;
        // 끊어진 덩어리 강조 - 편집마다 새로 만들지 않고 비워서 다시 채운다.
        private readonly Dictionary<int, Color> repairTints = new();

        private bool IsRepairing => currentPolicy != null && currentPolicy == repairPolicy;

        public void RegisterFieldFormationUI(SceneUIRoot sceneUIRoot, ICaravanRosterProvider rosterProvider, IFormationRepository formationRepository, IUnitConditionRepository conditionRepository, IFieldFormationActivityRepository activityRepository, IUIManager uiManager)
        {
            this.rosterProvider = rosterProvider;
            this.formationRepository = formationRepository;
            this.activityRepository = activityRepository;
            this.uiManager = uiManager;
            debugAreaSource = GetComponent<IFormationDebugAreaSource>();

            // 재등록(씬 재진입) 시 정리 모드가 남아 있지 않게 평소 정책으로 시작한다.
            timedPolicy = new FieldTimedEditPolicy(rosterProvider, formationRepository, activityRepository, GetAreaPins, ApplyAndCancelOutside);
            repairPolicy = new FieldRepairEditPolicy(rosterProvider, formationRepository, activityRepository, GetAreaPins);
            currentPolicy = timedPolicy;
            repairCompleted = null;
            repairGuideLabel = null;
            repairDoneButton = null;

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

            BindRepairElements(sceneUIRoot);

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
        // 순수 "숨기기"만 한다 - 정리 모드 [완료]도 패널을 직접 닫지 않고 완료 콜백만 부르며, 닫기는 호출자가 UIManager.Close(PanelId)로 한다.
        public void Close() => gridEditor?.Close();

        // 필드 로딩 중 정비창 칸 미리 생성(Docs/설계/67번 §4.4).
        public ContentSceneId LoadingScene => ContentSceneId.Field;

        public IEnumerable<ISceneLoadingTask> GetLoadingTasks()
        {
            yield return new ActionSceneLoadingTask("정비창 준비 중", () => gridEditor?.Prewarm());
        }

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

        // 평소 2칸, 정리 모드는 마을과 같은 계산(정책이 결정).
        public int GetVisibleMarginCells() => currentPolicy?.GetVisibleMarginCells() ?? FormationVisibleMargin.Default;

        // 두 정책 모두 마차를 팔레트에서 뺀다(상행 중엔 마차를 새로 놓을 수 없음, 2026-09-29 사용자 결정).
        public bool ShowsInPalette(IFormationUnit unit) => currentPolicy?.ShowsInPalette(unit) ?? unit.Kind != FormationUnitKind.Wagon;

#if UNITY_EDITOR
        public bool HasDebugPinStore => debugAreaSource != null;

        // 핀은 대열 영역을 바꾸므로 정리 모드면 대열 상태 표시도 갱신한다.
        public void HandleDebugPinAdd(int slotIndex, FormationAreaShape shape)
        {
            debugAreaSource?.AddOrReplace(new FormationAreaPin(slotIndex, shape));
            AfterEdit(debugAreaSource != null);
        }

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
            return AfterEdit(true);
        }
#endif

        public bool IsUnitReserved(string unitId) => activityRepository != null && activityRepository.IsUnitBusy(unitId);

        // 편집 요청은 현재 정책에 위임한다(설계 79번 §8). 정리 모드에서는 반영될 때마다 [완료] 활성·끊어진 덩어리 표시를 갱신한다.
        public bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex) => AfterEdit(currentPolicy != null && currentPolicy.HandlePaletteDrop(unit, targetSlotIndex));

        public bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex) => AfterEdit(currentPolicy != null && currentPolicy.HandleGridMove(unitId, originSlotIndex, targetSlotIndex));

        public bool HandleRemove(string unitId, int slotIndex) => AfterEdit(currentPolicy != null && currentPolicy.HandleRemove(unitId, slotIndex));

        // 재조정 드롭은 편집기가 끝에 전체를 다시 그리지 않는 경로(도착 고스트 드래그)가 있다 - 정리 모드는 배치를 즉시 바꾸므로 여기서 다시 그린다.
        // 평소 정책은 활동만 바꾸고 다음 프레임 오버레이 갱신이 그리므로 기존대로 다시 그리지 않는다.
        public void HandleRedirectMove(string unitId, int newTargetSlotIndex)
        {
            if (currentPolicy == null || !currentPolicy.HandleRedirectMove(unitId, newTargetSlotIndex) || !IsRepairing) return;

            RefreshRepairState();
            gridEditor?.RequestRefresh();
        }

        private bool AfterEdit(bool accepted)
        {
            if (accepted && IsRepairing) RefreshRepairState();
            return accepted;
        }

        private void BindRepairElements(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<TMP_Text>(FormationUIElementIds.RepairGuideLabel, out repairGuideLabel))
            {
                Debug.LogWarning($"Formation UI에서 '{FormationUIElementIds.RepairGuideLabel}' 요소를 찾을 수 없다 - 정비창 정리 모드를 건너뛴다.");
            }
            if (!sceneUIRoot.TryGetElement<Button>(FormationUIElementIds.RepairDoneButton, out repairDoneButton))
            {
                Debug.LogWarning($"Formation UI에서 '{FormationUIElementIds.RepairDoneButton}' 요소를 찾을 수 없다 - 정비창 정리 모드를 건너뛴다.");
            }

            if (repairGuideLabel != null)
            {
                repairGuideLabel.text = RepairGuideText;
                repairGuideLabel.gameObject.SetActive(false);
            }
            if (repairDoneButton != null)
            {
                repairDoneButton.onClick.RemoveAllListeners();
                repairDoneButton.onClick.AddListener(HandleRepairDone);
                repairDoneButton.gameObject.SetActive(false);
            }
        }

        // 정리 모드 시작(설계 79번 §8). 호출자가 이미 UIManager로 정비창을 연 뒤 부른다 - 열 때는 평소 여백으로 그렸으므로 정책을 바꾼 뒤 다시 그린다.
        // 정리 중에는 닫기 버튼을 숨겨 [완료] 외의 경로로 빠져나갈 수 없게 한다. 화면 요소가 없으면 플로우가 멈추지 않게 곧바로 완료 처리한다.
        public bool CanRepair => gridEditor != null && closeButton != null && repairGuideLabel != null && repairDoneButton != null && repairPolicy != null;

        public void BeginRepair(Action onCompleted)
        {
            if (!CanRepair)
            {
                Debug.LogWarning($"{nameof(FieldFormationPanel)}: 정비창 정리 모드 화면 요소가 연결되지 않아 정리 단계를 건너뛴다.");
                onCompleted?.Invoke();
                return;
            }

            repairCompleted = onCompleted;
            currentPolicy = repairPolicy;
            closeButton.gameObject.SetActive(false);
            repairGuideLabel.text = RepairGuideText;
            repairGuideLabel.gameObject.SetActive(true);
            repairDoneButton.gameObject.SetActive(true);

            RefreshRepairState();
            gridEditor.RequestRefresh();
        }

        // 연결되기 전에는 버튼이 비활성이라 눌리지 않지만, 방어적으로 한 번 더 확인한다. 패널을 닫는 것은 호출자 몫(UIManager.Close)이다.
        private void HandleRepairDone()
        {
            if (!IsRepairing || !IsFormationConnected()) return;

            currentPolicy = timedPolicy;
            repairTints.Clear();
            gridEditor?.SetCellTints(null);
            repairGuideLabel.gameObject.SetActive(false);
            repairDoneButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(true);

            var completed = repairCompleted;
            repairCompleted = null;
            completed?.Invoke();
        }

        // [완료] 활성 = 마차 덩어리 1개 이하, 가장 큰 덩어리 외의 연결 칸 붉은 톤(기획 78번 §4-17·18).
        private void RefreshRepairState()
        {
            var area = ComputeCurrentArea();
            repairDoneButton.interactable = area.ComponentCount <= 1;
            FormationDisconnectedCells.CollectTints(area, FormationDisconnectedCells.DefaultTint, repairTints);
            gridEditor?.SetCellTints(repairTints);
        }

        private bool IsFormationConnected() => ComputeCurrentArea().ComponentCount <= 1;

        private FormationArea ComputeCurrentArea() => FormationArea.Compute(GetDisplayLayout(), FormationAreaRules.LookupFrom(rosterProvider), GetAreaPins());

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
    }
}
