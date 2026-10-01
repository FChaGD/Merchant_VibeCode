#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 상행 준비 지도의 디버그 편집(Docs/설계/69번 §6) - 도시·관문 배치, 이동·삭제, 도로 그리기, 지역 추가·삭제, 엑셀 저장. 예전
    /// TripMapInteractionCoordinator에서 편집 부분만 남긴 것이다. 지도 표시·도착지·지역 전환은 TripMapPresenter(정식)가 맡고, 이
    /// 클래스는 편집 계약(IWorldMapEditor)으로 모델만 바꾼다 - 표시는 모델 변경 이벤트로 프레젠터가 다시 그린다.
    /// 편집 규칙(관문 짝 자동 생성·동반 삭제, 같은 지역 도로, 지역 삭제 조건)은 모델이 지키고, 여기서는 입력과 확인 창만 다룬다.
    /// 디버그 도구를 걷어낼 때 이 파일과 TripPanel.RegisterDebugMapEditor, 인스톨러의 디버그 컨트롤을 함께 지운다.
    /// </summary>
    internal sealed class TripMapDebugEditor : IDisposable
    {
        private readonly TripDebugRoadModeController roadMode = new();
        private readonly List<int> gateTargetRegionIds = new();

        private TripMapPresenter presenter;
        private IWorldMapEditor editor;
        private ITripCurrentLocationReader currentLocation;
        private ITripDestinationAssigner destination;

        private TripDebugCityPaletteView cityPalette;
        private TripDebugCityPaletteView gatePalette;
        private TMP_Dropdown gateTargetDropdown;
        private Button cityBulkDeleteButton;
        private GameObject confirmPanel;
        private TMP_Text confirmMessage;
        private Button confirmOkButton;
        private Button confirmCancelButton;

        private Image dragGhost;
        private MoveCityDragBehavior moveBehavior;
        private DrawRoadDragBehavior drawRoadBehavior;
        private ICityDragBehavior activeDragBehavior;

        public bool TryBind(SceneUIRoot sceneUIRoot, TripMapPresenter presenter, IWorldMapEditor editor, ITripCurrentLocationReader currentLocation, ITripDestinationAssigner destination, Transform rootCanvasTransform)
        {
            var ok = TryGet(sceneUIRoot, TripUIElementIds.DebugCityPaletteRoot, out cityPalette)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugGatePaletteRoot, out gatePalette)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugGateTargetDropdown, out gateTargetDropdown)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugRoadToggleButton, out TripDebugRoadToggleView roadToggle)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugCityBulkDeleteButton, out cityBulkDeleteButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugRoadBulkDeleteButton, out Button roadBulkDeleteButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugMapSaveButton, out Button saveButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugAddRegionButton, out Button addRegionButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugRemoveRegionButton, out Button removeRegionButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugConfirmPanel, out RectTransform confirmRoot)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugConfirmMessage, out confirmMessage)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugConfirmOkButton, out confirmOkButton)
                & TryGet(sceneUIRoot, TripUIElementIds.DebugConfirmCancelButton, out confirmCancelButton);
            if (!ok) return false;

            this.presenter = presenter;
            this.editor = editor;
            this.currentLocation = currentLocation;
            this.destination = destination;
            confirmPanel = confirmRoot.gameObject;
            confirmPanel.SetActive(false);

            var mapView = presenter.MapView;
            moveBehavior = new MoveCityDragBehavior(editor, mapView);
            drawRoadBehavior = new DrawRoadDragBehavior(editor, mapView, presenter.CreateStandaloneLine, ResolveNodeUnderPointer);
            presenter.SetEditHandlers(HandleMarkerBeginDrag, HandleMarkerDrag, HandleMarkerEndDrag, (a, b) => editor.RemoveRoad(a, b));

            CreateDragGhost(rootCanvasTransform);
            cityPalette.Initialize(eventData => BeginPaletteDrag(cityPalette, eventData), MovePaletteDrag, eventData => EndPaletteDrag(eventData, isGate: false));
            gatePalette.Initialize(eventData => BeginPaletteDrag(gatePalette, eventData), MovePaletteDrag, eventData => EndPaletteDrag(eventData, isGate: true));

            roadToggle.Initialize(() => roadMode.Toggle());
            roadToggle.SetActiveVisual(roadMode.IsRoadModeActive);
            roadMode.Changed += isActive =>
            {
                roadToggle.SetActiveVisual(isActive);
                cityBulkDeleteButton.interactable = !isActive; // 도로 모드 중에는 도시 삭제 전면 불가(기존 규칙)
            };

            // 전체 삭제는 보이는 지역만(설계 69번 §6).
            cityBulkDeleteButton.onClick.RemoveAllListeners();
            cityBulkDeleteButton.onClick.AddListener(() =>
            {
                if (!roadMode.IsRoadModeActive) editor.ClearCities(presenter.ViewedRegionId);
            });
            roadBulkDeleteButton.onClick.RemoveAllListeners();
            roadBulkDeleteButton.onClick.AddListener(() => editor.ClearRoads(presenter.ViewedRegionId));

            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(() => TripCityMapPersistence.Save(editor));

            addRegionButton.onClick.RemoveAllListeners();
            addRegionButton.onClick.AddListener(() => presenter.ShowRegion(editor.AddRegion()));
            removeRegionButton.onClick.RemoveAllListeners();
            removeRegionButton.onClick.AddListener(RequestRemoveRegion);
            confirmCancelButton.onClick.RemoveAllListeners();
            confirmCancelButton.onClick.AddListener(() => confirmPanel.SetActive(false));

            editor.CityRemoved += destination.HandleCityDeleted; // 지워진 도시가 도착지였으면 해제(기획 16번 §6)
            editor.Changed += RefreshGateTargets;
            presenter.ViewedRegionChanged += HandleViewedRegionChanged;
            RefreshGateTargets();
            return true;
        }

        public void Dispose()
        {
            if (editor != null)
            {
                editor.CityRemoved -= destination.HandleCityDeleted;
                editor.Changed -= RefreshGateTargets;
            }
            if (presenter != null) presenter.ViewedRegionChanged -= HandleViewedRegionChanged;
        }

        // ==================== 관문 대상 지역 드롭다운 ====================

        private void HandleViewedRegionChanged(int regionId)
        {
            confirmPanel.SetActive(false);
            RefreshGateTargets();
        }

        // 보이는 지역을 뺀 지역 목록(기획 68번 §3.5). 다른 지역이 없으면 관문 팔레트를 막는다.
        private void RefreshGateTargets()
        {
            gateTargetRegionIds.Clear();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var region in editor.Regions)
            {
                if (region.Id == presenter.ViewedRegionId) continue;
                gateTargetRegionIds.Add(region.Id);
                options.Add(new TMP_Dropdown.OptionData(region.Name));
            }

            var previous = gateTargetDropdown.value;
            gateTargetDropdown.ClearOptions();
            gateTargetDropdown.AddOptions(options);
            gateTargetDropdown.SetValueWithoutNotify(Mathf.Clamp(previous, 0, Mathf.Max(0, options.Count - 1)));
            gateTargetDropdown.RefreshShownValue();
            gateTargetDropdown.interactable = options.Count > 0;
            gatePalette.enabled = options.Count > 0;
        }

        // ==================== 팔레트 드래그 ====================

        private void CreateDragGhost(Transform rootCanvasTransform)
        {
            if (rootCanvasTransform == null) return;

            var go = new GameObject("TripDebugMapDragGhost", typeof(RectTransform));
            go.transform.SetParent(rootCanvasTransform, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            dragGhost = image;
        }

        // 고스트는 지도 content 밖(루트 캔버스)이라 줌 배율을 물려받지 못한다 - 드래그 시작마다 마커 크기 × 현재 배율로 맞춘다.
        private void BeginPaletteDrag(TripDebugCityPaletteView palette, PointerEventData eventData)
        {
            if (dragGhost == null) return;

            var mapView = presenter.MapView;
            var zoom = mapView.Content != null ? mapView.Content.localScale.x : 1f;
            dragGhost.sprite = palette.Icon;
            dragGhost.rectTransform.sizeDelta = presenter.MarkerSize * zoom;
            dragGhost.gameObject.SetActive(true);
            dragGhost.transform.SetAsLastSibling();
            dragGhost.transform.position = eventData.position;
        }

        private void MovePaletteDrag(PointerEventData eventData)
        {
            if (dragGhost != null) dragGhost.transform.position = eventData.position;
        }

        private void EndPaletteDrag(PointerEventData eventData, bool isGate)
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);

            var mapView = presenter.MapView;
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapView.Viewport, eventData.position, eventData.pressEventCamera)) return; // 무효 드롭 = 취소
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapView.Content, eventData.position, eventData.pressEventCamera, out var localPoint)) return;

            if (!isGate)
            {
                editor.AddCity(presenter.ViewedRegionId, localPoint);
                return;
            }

            var index = gateTargetDropdown.value;
            if (index < 0 || index >= gateTargetRegionIds.Count) return;
            editor.AddGatePair(presenter.ViewedRegionId, localPoint, gateTargetRegionIds[index]);
        }

        // ==================== 마커 드래그 ====================

        private void HandleMarkerBeginDrag(TripMapMarkerView marker, PointerEventData eventData)
        {
            activeDragBehavior = roadMode.IsRoadModeActive ? drawRoadBehavior : moveBehavior;
            activeDragBehavior.OnDragBegin(marker, eventData);
        }

        private void HandleMarkerDrag(PointerEventData eventData) => activeDragBehavior?.OnDragUpdate(eventData);

        private void HandleMarkerEndDrag(PointerEventData eventData)
        {
            activeDragBehavior?.OnDragEnd(eventData);
            activeDragBehavior = null;
        }

        private static MapNodeId? ResolveNodeUnderPointer(PointerEventData eventData)
        {
            var target = eventData.pointerCurrentRaycast.gameObject;
            var marker = target != null ? target.GetComponentInParent<TripMapMarkerView>() : null;
            return marker != null ? marker.Node : null;
        }

        // ==================== 지역 삭제 ====================

        // 거부 조건이면 사유만, 내용이 있으면 확인 후, 빈 지역이면 바로 삭제한다(기획 68번 §3.6).
        private void RequestRemoveRegion()
        {
            var regionId = presenter.ViewedRegionId;
            var check = editor.CheckRegionRemoval(regionId, currentLocation.CurrentCityId);
            if (check != RegionRemovalCheck.Allowed)
            {
                ShowConfirm(check switch
                {
                    RegionRemovalCheck.LastRegion => "마지막 지역은 삭제할 수 없습니다.",
                    RegionRemovalCheck.HasCurrentLocation => "현재 위치가 있는 지역은 삭제할 수 없습니다.",
                    _ => "지역을 찾을 수 없습니다.",
                }, onConfirm: null);
                return;
            }

            var cityCount = editor.GetCities(regionId).Count();
            var gateCount = editor.GetGates(regionId).Count();
            if (cityCount == 0 && gateCount == 0)
            {
                RemoveRegion(regionId);
                return;
            }

            ShowConfirm($"도시 {cityCount}개·관문 {gateCount}개가 함께 삭제됩니다.", () => RemoveRegion(regionId));
        }

        private void RemoveRegion(int regionId)
        {
            if (!editor.RemoveRegion(regionId, currentLocation.CurrentCityId)) return;
            var home = editor.TryGetCity(currentLocation.CurrentCityId, out var city) ? city.RegionId : editor.Regions[0].Id;
            presenter.ShowRegion(home);
        }

        // 확인 버튼 하나로 알림(onConfirm 없음)과 확인(있음)을 겸한다 - 알림일 때는 취소 버튼을 숨긴다.
        private void ShowConfirm(string message, Action onConfirm)
        {
            confirmMessage.text = message;
            confirmCancelButton.gameObject.SetActive(onConfirm != null);
            confirmOkButton.onClick.RemoveAllListeners();
            confirmOkButton.onClick.AddListener(() =>
            {
                confirmPanel.SetActive(false);
                onConfirm?.Invoke();
            });
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
        }

        private static bool TryGet<T>(SceneUIRoot sceneUIRoot, string id, out T component) where T : Component
        {
            if (sceneUIRoot.TryGetElement(id, out component)) return true;
            Debug.LogWarning($"{nameof(TripMapDebugEditor)}: 지도 디버그 요소 '{id}'({typeof(T).Name})를 찾을 수 없어 지도 편집을 건너뛴다(Tools > Game > Build Hub Scene).");
            return false;
        }
    }
}
#endif
