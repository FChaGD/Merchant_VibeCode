using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Hub 씬이 로드된 시점에 Hub 씬의 SceneUIRoot를 찾아 루트 depth(RootDepth)
    /// 버튼 클릭 동작과 배경 이미지를 연결한다.
    /// 루트 depth 버튼은 개별로 토글하지 않고 RootDepth 그룹 하나로 다룬다(Docs/설계/37번 §3.3) - 표시는
    /// 그룹 SetActive, 씬 전환 커튼 중 비활성은 그룹 CanvasGroup.interactable. 그래서 루트 depth에 버튼이
    /// 늘어도 이 클래스의 토글 코드는 바뀌지 않는다. PersistentLayer(재화 HUD 등)는 참조하지 않는다.
    /// </summary>
    public class HubUIController : MonoBehaviour, IHubUIController
    {
        [SerializeField] private Sprite backgroundSprite;

        /// <summary>
        /// Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 슬라이드시킬 대상 - Background+
        /// 두 레이어 전체를 감싸는 루트다. RegisterHubUI가 끝나야 값이 채워진다(Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
        /// </summary>
        public RectTransform ContentRoot { get; private set; }

        private GameObject rootDepth;
        private CanvasGroup rootDepthCanvasGroup;
        private TownButtonColumnView townCategoryColumn;
        private readonly Dictionary<string, GameObject> townCategoryButtonsById = new();
        private readonly List<GameObject> visibleCategoryBuffer = new();
        private CurrentTownFacilityFilter townFacilityFilter;
        private ISceneRevealSignal sceneRevealSignal;
        private IUIManager uiManager;

        public void RegisterHubUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, ISceneRevealSignal sceneRevealSignal, CurrentTownFacilityFilter townFacilityFilter)
        {
            this.uiManager = uiManager;
            this.sceneRevealSignal = sceneRevealSignal;

            if (!sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.ContentRoot, out var contentRoot))
            {
                WarnMissing(HubUIElementIds.ContentRoot);
            }
            ContentRoot = contentRoot;

            rootDepth = null;
            rootDepthCanvasGroup = null;
            if (sceneUIRoot.TryGetElement<CanvasGroup>(HubUIElementIds.RootDepth, out var canvasGroup))
            {
                rootDepthCanvasGroup = canvasGroup;
                rootDepth = canvasGroup.gameObject;
            }
            else
            {
                WarnMissing(HubUIElementIds.RootDepth);
            }

            BindButton(sceneUIRoot, HubUIElementIds.DepartureButton, () => uiManager.Open(UIPanelIds.Trip));
            BindButton(sceneUIRoot, HubUIElementIds.FormationButton, () => uiManager.Open(UIPanelIds.Formation));
            BindButton(sceneUIRoot, HubUIElementIds.TacticsButton, () => uiManager.Open(UIPanelIds.Tactics));
            BindTownCategories(sceneUIRoot, townFacilityFilter);

            // 화면이 완전히 드러나기 전까지는 루트 depth 전체 비활성 - 전환 없이 로드된 경우(최초 진입 등)엔
            // SceneRevealed가 즉시 발생해 사실상 바로 다시 활성화된다.
            SetRootDepthInteractable(false);
            sceneRevealSignal.SceneRevealed -= HandleSceneRevealed;
            sceneRevealSignal.SceneRevealed += HandleSceneRevealed;

            // 패널(depth)이 하나라도 열려있는 동안은 루트 depth를 완전히 숨긴다(사용자 확정, 2026-09-07).
            // 패널끼리 중첩 전환되는 동안(상행 준비→배치→복귀)에는 계속 숨김 상태가 유지되고, 최상위
            // 패널까지 완전히 닫혀야 다시 나타난다(IUIManager.OnAnyPanelOpenChanged 참고).
            uiManager.OnAnyPanelOpenChanged -= HandleAnyPanelOpenChanged;
            uiManager.OnAnyPanelOpenChanged += HandleAnyPanelOpenChanged;

            ApplyBackground(sceneUIRoot);
        }

        private void BindTownCategories(SceneUIRoot sceneUIRoot, CurrentTownFacilityFilter filter)
        {
            // 이전 필터로 구독했던 핸들러를 먼저 뗀다 - 구독은 필터가 아니라 현재 위치 저장소(영속)에
            // 걸려 있으므로 새 필터로 -= 해도 같은 핸들러가 제거된다(CurrentTownFacilityFilter.Changed 참고).
            if (townFacilityFilter != null)
            {
                townFacilityFilter.Changed -= ArrangeTownCategories;
            }
            townFacilityFilter = filter;

            townCategoryButtonsById.Clear();
            if (!sceneUIRoot.TryGetElement<TownButtonColumnView>(HubUIElementIds.TownCategoryColumn, out townCategoryColumn))
            {
                WarnMissing(HubUIElementIds.TownCategoryColumn);
                return;
            }

            foreach (var categoryId in TownFacilityCatalog.CategoryIds)
            {
                var capturedId = categoryId;
                var button = BindButton(sceneUIRoot, HubUIElementIds.TownCategoryButton(categoryId), () => uiManager.Open(UIPanelIds.TownCategory(capturedId)));
                if (button != null)
                {
                    townCategoryButtonsById[categoryId] = button.gameObject;
                }
            }

            ArrangeTownCategories();
            // 마을 이동은 항상 Field→Hub 씬 재로드를 거치므로 등록 시 1회 배치로 충분하지만, Hub에 머무는
            // 동안 현재 위치가 바뀌는 경로가 생겨도 맞게 따라가도록 방어적으로 구독한다(Docs/설계/37번 §5.3).
            townFacilityFilter.Changed += ArrangeTownCategories;
        }

        // 제공 시설이 1개 이상인 카테고리만 위에서부터 빈칸 없이 채운다(Docs/설계/37번 §5).
        private void ArrangeTownCategories()
        {
            if (townCategoryColumn == null)
            {
                return;
            }

            visibleCategoryBuffer.Clear();
            foreach (var categoryId in TownFacilityCatalog.CategoryIds)
            {
                if (townCategoryButtonsById.TryGetValue(categoryId, out var button) && townFacilityFilter.IsCategoryAvailable(categoryId))
                {
                    visibleCategoryBuffer.Add(button);
                }
            }

            townCategoryColumn.Arrange(visibleCategoryBuffer);
        }

        private void HandleSceneRevealed(ContentSceneId sceneId)
        {
            if (sceneId != ContentSceneId.Hub)
            {
                return;
            }

            SetRootDepthInteractable(true);
        }

        private void HandleAnyPanelOpenChanged(bool isAnyPanelOpen)
        {
            if (rootDepth != null)
            {
                rootDepth.SetActive(!isAnyPanelOpen);
            }
        }

        private void SetRootDepthInteractable(bool interactable)
        {
            if (rootDepthCanvasGroup != null)
            {
                rootDepthCanvasGroup.interactable = interactable;
            }
        }

        private void ApplyBackground(SceneUIRoot sceneUIRoot)
        {
            if (backgroundSprite == null)
            {
                return;
            }

            if (!sceneUIRoot.TryGetElement<Image>(HubUIElementIds.Background, out var background))
            {
                Debug.LogWarning($"Hub UI에서 배경 Image('{HubUIElementIds.Background}')를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
                return;
            }

            background.sprite = backgroundSprite;
        }

        private static Button BindButton(SceneUIRoot sceneUIRoot, string id, UnityAction action)
        {
            if (!sceneUIRoot.TryGetElement<Button>(id, out var button))
            {
                WarnMissing(id);
                return null;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            return button;
        }

        private static void WarnMissing(string id)
        {
            Debug.LogWarning($"Hub UI에서 '{id}' 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
        }

        private void OnDestroy()
        {
            if (townFacilityFilter != null)
            {
                townFacilityFilter.Changed -= ArrangeTownCategories;
            }
        }
    }
}
