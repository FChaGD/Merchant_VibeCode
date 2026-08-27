using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 방향성 지시 UI를 조율한다. FormationPanel과 같은 이유로 Hub/Field 콘텐츠 씬이 로드될 때마다
    /// RegisterTacticsUI가 다시 호출되어 그 씬의 화면 요소로 재바인딩된다. 파티 3축은 즉시 반영
    /// (Apply 버튼 없음 - 정비창과 달리 조합 실수로 되돌릴 이유가 없는 단순 프리셋 선택이라 별도
    /// 확인 단계를 두지 않았다), 역할군 override 토글은 화면에 존재하되 비활성 상태로 렌더링한다
    /// (Docs/설계/12번 §6-1 - 개체별 방향성이 생기기 전까지는 기능을 구현하지 않기로 확정).
    /// </summary>
    public class TacticsPanel : MonoBehaviour, ITacticsPanel
    {
        // 역할군 후보+라벨 - InMemoryTacticsRepository/LiveBattleSimulationRule과 같은 에셋 파일을
        // 인스펙터에서 각자 참조한다(같은 자리, 같은 패턴).
        [SerializeField] private RoleGroupTacticsCatalogAsset catalog;

        public string PanelId => UIPanelIds.Tactics;

        private static readonly (EnemyRecognitionType Value, string Label)[] RecognitionOptions =
        {
            (EnemyRecognitionType.OneSecondDelay, "1초 지연"),
            (EnemyRecognitionType.FiveSecondOrProximity, "5초 지연 또는 근접"),
            (EnemyRecognitionType.ProximityOrHit, "근접 또는 피격"),
        };

        private static readonly (ActivityRadiusPreset Value, string Label)[] RadiusOptions =
        {
            (ActivityRadiusPreset.Fixed, "고정(4m)"),
            (ActivityRadiusPreset.Standard, "표준"),
            (ActivityRadiusPreset.Wide, "광역"),
        };

        private static readonly (PursuitPreset Value, string Label)[] PursuitOptions =
        {
            (PursuitPreset.Autonomous, "자율행동"),
            (PursuitPreset.HuntToKill, "추살"),
            (PursuitPreset.OffensiveJudgment, "공세판단"),
            (PursuitPreset.NoPursuit, "추적금지"),
            (PursuitPreset.HoldPosition, "위치사수"),
        };

        private GameObject panelRoot;
        private Button closeButton;
        private Button tabPartyButton;
        private Button tabRoleGroupButton;
        private Button tabIndividualButton;
        private GameObject partyContentRoot;
        private GameObject roleGroupContentRoot;
        private GameObject individualContentRoot;

        private TMP_Dropdown recognitionDropdown;
        private TMP_Dropdown radiusDropdown;
        private TMP_Dropdown pursuitDropdown;

        private Toggle frontlineOverrideToggle;
        private TMP_Dropdown frontlineTargetDropdown;
        private TMP_Dropdown frontlinePositioningDropdown;
        private TMP_Dropdown frontlineSelfPreservationDropdown;

        private Toggle rangedOverrideToggle;
        private TMP_Dropdown rangedTargetDropdown;
        private TMP_Dropdown rangedPositioningDropdown;
        private TMP_Dropdown rangedSelfPreservationDropdown;

        private ITacticsRepository repository;
        private IUIManager uiManager;

        public void RegisterTacticsUI(ITacticsRepository repository, IUIManager uiManager, string sceneName)
        {
            // repository가 없으면(예: 인스톨러를 아직 재실행하지 않아 InMemoryTacticsRepository가
            // DI에 없는 과도기) 조용히 건너뛴다 - FormationPanel이 IFormationRepository 부재를
            // 다루는 것과 같은 방향(Open()/드롭다운 콜백에서 NPE가 나지 않도록 아예 바인딩을 안 함).
            if (repository == null)
            {
                Debug.LogWarning($"{nameof(ITacticsRepository)}가 연결되어 있지 않아 방향성 지시 UI를 등록하지 못했다.");
                return;
            }

            this.repository = repository;
            this.uiManager = uiManager;

            var contentScene = SceneManager.GetSceneByName(sceneName);
            if (!contentScene.IsValid())
            {
                Debug.LogWarning($"'{sceneName}' 씬을 찾을 수 없어 방향성 지시 UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in contentScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{sceneName}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            if (!TryBind(sceneUIRoot))
            {
                return;
            }

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => uiManager.Close(PanelId));

            tabPartyButton.onClick.RemoveAllListeners();
            tabPartyButton.onClick.AddListener(() => SwitchTab(TacticsTab.Party));
            tabRoleGroupButton.onClick.RemoveAllListeners();
            tabRoleGroupButton.onClick.AddListener(() => SwitchTab(TacticsTab.RoleGroup));
            tabIndividualButton.onClick.RemoveAllListeners();
            tabIndividualButton.onClick.AddListener(() => SwitchTab(TacticsTab.Individual));

            BindPartyDropdowns();
            BindRoleGroupDropdowns(RoleGroup.Frontline, frontlineOverrideToggle, frontlineTargetDropdown, frontlinePositioningDropdown, frontlineSelfPreservationDropdown);
            BindRoleGroupDropdowns(RoleGroup.RangedDealer, rangedOverrideToggle, rangedTargetDropdown, rangedPositioningDropdown, rangedSelfPreservationDropdown);

            panelRoot.SetActive(false);
        }

        private bool TryBind(SceneUIRoot sceneUIRoot)
        {
            if (!sceneUIRoot.TryGetElement<Transform>(TacticsUIElementIds.PanelRoot, out var rootTransform)) return WarnMissing(TacticsUIElementIds.PanelRoot);
            panelRoot = rootTransform.gameObject;

            if (!sceneUIRoot.TryGetElement<Button>(TacticsUIElementIds.CloseButton, out closeButton)) return WarnMissing(TacticsUIElementIds.CloseButton);

            if (!sceneUIRoot.TryGetElement<Button>(TacticsUIElementIds.TabParty, out tabPartyButton)) return WarnMissing(TacticsUIElementIds.TabParty);
            if (!sceneUIRoot.TryGetElement<Button>(TacticsUIElementIds.TabRoleGroup, out tabRoleGroupButton)) return WarnMissing(TacticsUIElementIds.TabRoleGroup);
            if (!sceneUIRoot.TryGetElement<Button>(TacticsUIElementIds.TabIndividual, out tabIndividualButton)) return WarnMissing(TacticsUIElementIds.TabIndividual);

            if (!sceneUIRoot.TryGetElement<Transform>(TacticsUIElementIds.PartyContentRoot, out var partyRoot)) return WarnMissing(TacticsUIElementIds.PartyContentRoot);
            partyContentRoot = partyRoot.gameObject;
            if (!sceneUIRoot.TryGetElement<Transform>(TacticsUIElementIds.RoleGroupContentRoot, out var roleGroupRoot)) return WarnMissing(TacticsUIElementIds.RoleGroupContentRoot);
            roleGroupContentRoot = roleGroupRoot.gameObject;
            if (!sceneUIRoot.TryGetElement<Transform>(TacticsUIElementIds.IndividualContentRoot, out var individualRoot)) return WarnMissing(TacticsUIElementIds.IndividualContentRoot);
            individualContentRoot = individualRoot.gameObject;

            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.RecognitionDropdown, out recognitionDropdown)) return WarnMissing(TacticsUIElementIds.RecognitionDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.RadiusDropdown, out radiusDropdown)) return WarnMissing(TacticsUIElementIds.RadiusDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.PursuitDropdown, out pursuitDropdown)) return WarnMissing(TacticsUIElementIds.PursuitDropdown);

            if (!sceneUIRoot.TryGetElement<Toggle>(TacticsUIElementIds.FrontlineOverrideToggle, out frontlineOverrideToggle)) return WarnMissing(TacticsUIElementIds.FrontlineOverrideToggle);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.FrontlineTargetDropdown, out frontlineTargetDropdown)) return WarnMissing(TacticsUIElementIds.FrontlineTargetDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.FrontlinePositioningDropdown, out frontlinePositioningDropdown)) return WarnMissing(TacticsUIElementIds.FrontlinePositioningDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.FrontlineSelfPreservationDropdown, out frontlineSelfPreservationDropdown)) return WarnMissing(TacticsUIElementIds.FrontlineSelfPreservationDropdown);

            if (!sceneUIRoot.TryGetElement<Toggle>(TacticsUIElementIds.RangedOverrideToggle, out rangedOverrideToggle)) return WarnMissing(TacticsUIElementIds.RangedOverrideToggle);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.RangedTargetDropdown, out rangedTargetDropdown)) return WarnMissing(TacticsUIElementIds.RangedTargetDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.RangedPositioningDropdown, out rangedPositioningDropdown)) return WarnMissing(TacticsUIElementIds.RangedPositioningDropdown);
            if (!sceneUIRoot.TryGetElement<TMP_Dropdown>(TacticsUIElementIds.RangedSelfPreservationDropdown, out rangedSelfPreservationDropdown)) return WarnMissing(TacticsUIElementIds.RangedSelfPreservationDropdown);

            // 오버라이드 토글은 화면에 존재하되 비활성 - 개체별 방향성이 생기기 전까지는 누를 대상이
            // 없다(Docs/설계/12번 §6-1).
            frontlineOverrideToggle.interactable = false;
            rangedOverrideToggle.interactable = false;

            return true;
        }

        private static bool WarnMissing(string id)
        {
            Debug.LogWarning($"방향성 지시 UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
            return false;
        }

        public void Open()
        {
            if (panelRoot == null) return;

            RefreshFromRepository();
            SwitchTab(TacticsTab.Party);
            panelRoot.SetActive(true);
        }

        // 순수 "숨기기"만 한다. FormationPanel/TripPanel과 같은 규칙 - 다른 패널로의 네비게이션은
        // UIManager.Close(PanelId)의 책임이므로 이 메서드를 외부에서 직접 호출하지 말 것.
        public void Close()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
        }

        private void SwitchTab(TacticsTab tab)
        {
            partyContentRoot.SetActive(tab == TacticsTab.Party);
            roleGroupContentRoot.SetActive(tab == TacticsTab.RoleGroup);
            individualContentRoot.SetActive(tab == TacticsTab.Individual);
        }

        private void RefreshFromRepository()
        {
            var party = repository.GetPartySettings();
            SelectWithoutNotify(recognitionDropdown, IndexOf(RecognitionOptions, party.RecognitionType));
            SelectWithoutNotify(radiusDropdown, IndexOf(RadiusOptions, party.RadiusPreset));
            SelectWithoutNotify(pursuitDropdown, IndexOf(PursuitOptions, party.Pursuit));

            RefreshRoleGroupSelection(RoleGroup.Frontline, frontlineTargetDropdown, frontlinePositioningDropdown, frontlineSelfPreservationDropdown);
            RefreshRoleGroupSelection(RoleGroup.RangedDealer, rangedTargetDropdown, rangedPositioningDropdown, rangedSelfPreservationDropdown);
        }

        private void RefreshRoleGroupSelection(RoleGroup roleGroup, TMP_Dropdown targetDropdown, TMP_Dropdown positioningDropdown, TMP_Dropdown selfPreservationDropdown)
        {
            if (catalog == null || !catalog.TryGetEntry(roleGroup, out var entry)) return;

            var current = repository.GetRoleGroupOverride(roleGroup);
            SelectWithoutNotify(targetDropdown, IndexOfOption(entry.TargetPriorityOptions, current.TargetPriority));
            SelectWithoutNotify(positioningDropdown, IndexOfOption(entry.PositioningOptions, current.Positioning));
            SelectWithoutNotify(selfPreservationDropdown, IndexOfOption(entry.SelfPreservationOptions, current.SelfPreservation));
        }

        private void BindPartyDropdowns()
        {
            SetOptions(recognitionDropdown, RecognitionOptions);
            recognitionDropdown.onValueChanged.RemoveAllListeners();
            recognitionDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetPartySettings();
                repository.SetPartySettings(new PartyTacticsSettings(RecognitionOptions[index].Value, current.RadiusPreset, current.Pursuit));
            });

            SetOptions(radiusDropdown, RadiusOptions);
            radiusDropdown.onValueChanged.RemoveAllListeners();
            radiusDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetPartySettings();
                repository.SetPartySettings(new PartyTacticsSettings(current.RecognitionType, RadiusOptions[index].Value, current.Pursuit));
            });

            SetOptions(pursuitDropdown, PursuitOptions);
            pursuitDropdown.onValueChanged.RemoveAllListeners();
            pursuitDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetPartySettings();
                repository.SetPartySettings(new PartyTacticsSettings(current.RecognitionType, current.RadiusPreset, PursuitOptions[index].Value));
            });
        }

        private void BindRoleGroupDropdowns(
            RoleGroup roleGroup, Toggle overrideToggle,
            TMP_Dropdown targetDropdown, TMP_Dropdown positioningDropdown, TMP_Dropdown selfPreservationDropdown)
        {
            if (catalog == null || !catalog.TryGetEntry(roleGroup, out var entry))
            {
                Debug.LogWarning($"{nameof(TacticsPanel)}: '{roleGroup}' 역할군의 후보를 카탈로그에서 찾을 수 없다({nameof(RoleGroupTacticsCatalogAsset)}가 연결됐는지 확인).");
                return;
            }

            SetOptions(targetDropdown, entry.TargetPriorityOptions, option => option.DisplayLabel);
            targetDropdown.onValueChanged.RemoveAllListeners();
            targetDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetRoleGroupOverride(roleGroup);
                repository.SetRoleGroupOverride(roleGroup, new RoleGroupTacticsOverride(current.IsOverridden, entry.TargetPriorityOptions[index].Value, current.Positioning, current.SelfPreservation));
            });

            SetOptions(positioningDropdown, entry.PositioningOptions, option => option.DisplayLabel);
            positioningDropdown.onValueChanged.RemoveAllListeners();
            positioningDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetRoleGroupOverride(roleGroup);
                repository.SetRoleGroupOverride(roleGroup, new RoleGroupTacticsOverride(current.IsOverridden, current.TargetPriority, entry.PositioningOptions[index].Value, current.SelfPreservation));
            });

            SetOptions(selfPreservationDropdown, entry.SelfPreservationOptions, option => option.DisplayLabel);
            selfPreservationDropdown.onValueChanged.RemoveAllListeners();
            selfPreservationDropdown.onValueChanged.AddListener(index =>
            {
                var current = repository.GetRoleGroupOverride(roleGroup);
                repository.SetRoleGroupOverride(roleGroup, new RoleGroupTacticsOverride(current.IsOverridden, current.TargetPriority, current.Positioning, entry.SelfPreservationOptions[index].Value));
            });
        }

        private static void SetOptions<T>(TMP_Dropdown dropdown, IReadOnlyList<T> options, System.Func<T, string> labelSelector)
        {
            var optionData = new List<TMP_Dropdown.OptionData>(options.Count);
            foreach (var option in options)
            {
                optionData.Add(new TMP_Dropdown.OptionData(labelSelector(option)));
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(optionData);
        }

        private static void SetOptions<TEnum>(TMP_Dropdown dropdown, (TEnum Value, string Label)[] options)
        {
            var optionData = new List<TMP_Dropdown.OptionData>(options.Length);
            foreach (var option in options)
            {
                optionData.Add(new TMP_Dropdown.OptionData(option.Label));
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(optionData);
        }

        private static void SelectWithoutNotify(TMP_Dropdown dropdown, int index)
        {
            if (index < 0) return;
            dropdown.SetValueWithoutNotify(index);
            dropdown.RefreshShownValue();
        }

        private static int IndexOf<TEnum>((TEnum Value, string Label)[] options, TEnum value)
        {
            for (var i = 0; i < options.Length; i++)
            {
                if (Equals(options[i].Value, value)) return i;
            }
            return -1;
        }

        private static int IndexOfOption(IReadOnlyList<TargetPriorityOption> options, TargetPriority value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Value == value) return i;
            }
            return -1;
        }

        private static int IndexOfOption(IReadOnlyList<LocalPositioningOption> options, LocalPositioning value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Value == value) return i;
            }
            return -1;
        }

        private static int IndexOfOption(IReadOnlyList<SelfPreservationOption> options, SelfPreservation value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Value == value) return i;
            }
            return -1;
        }
    }
}
