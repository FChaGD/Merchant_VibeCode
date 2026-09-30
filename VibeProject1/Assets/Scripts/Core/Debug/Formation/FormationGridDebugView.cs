#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 정비창 디버그 패널 - 디버그 핀으로 대열을 임의로 만들고 지운다(Docs/기획/59번 §4.5, 설계 60번 §8). 예전의 판 열·행·칸 크기 조절은
    /// 외곽 판이 50 × 50으로 고정되며 없앴다. 패널은 입력 파싱·핀 표시·클릭 중계만 하고, 핀 추가·제거의 규칙 판정(마차 연결 확인)은
    /// FormationGridEditor → 편집 정책이 담당한다.
    /// 격자 위 핀 표시는 격자 콘텐츠 아래에 직접 만든다 - 칸과 함께 스크롤·확대돼야 하기 때문이다(GridLayoutGroup 배치에서는 제외).
    /// </summary>
    public class FormationGridDebugView : MonoBehaviour
    {
        // 기본 핀 모양 = 9 × 9 전부 대열, 가운데 기준 칸(기획 65번 §3.3 - 예전 반경 4와 같음).
        private const int DefaultReach = 4;
        // 핀 표시 크기 = 칸 크기 비율(최소 8px) - 고정 px면 축소 시 칸보다 커져 이웃 칸을 덮는다.
        private const float MarkerToCellRatio = 0.3f;
        private const float MinMarkerSize = 8f;
        private static readonly Color MarkerColor = new(0.85f, 0.1f, 0.85f, 0.95f);

        // 핀 영역 모양 마스크 입력(기획 65번 §3.3) - 마차·시설과 같은 형식. 형식 오류는 안내 문구로 알린다.
        [SerializeField] private TMP_InputField shapeInput;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private FormationDebugPinHandle pinHandle;

        private readonly List<Button> markers = new();
        private Action<int, FormationAreaShape> onPinDropped;
        private Action<int> onPinClicked;

        public void Initialize(Action<int, FormationAreaShape> pinDropped, Action<int> pinClicked)
        {
            onPinDropped = pinDropped;
            onPinClicked = pinClicked;

            if (shapeInput != null && string.IsNullOrEmpty(shapeInput.text)) shapeInput.text = FormationAreaShape.Square(DefaultReach).ToString();
            if (messageLabel != null) messageLabel.text = string.Empty;
            if (pinHandle != null) pinHandle.Initialize(HandlePinDropped);
        }

        /// <summary>핀 칸마다 표시를 하나씩 그린다. 표시를 누르면 그 핀 제거를 시도한다.</summary>
        public void RenderPins(IReadOnlyList<FormationAreaPin> pins, RectTransform gridContent, Func<int, bool> isVisible, Func<int, Vector2> slotPosition, float cellSize)
        {
            var markerSize = Mathf.Max(MinMarkerSize, cellSize * MarkerToCellRatio);
            var count = pins?.Count ?? 0;
            while (markers.Count < count) markers.Add(CreateMarker(gridContent));

            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (i >= count || !isVisible(pins[i].SlotIndex))
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                var slotIndex = pins[i].SlotIndex;
                marker.gameObject.SetActive(true);
                marker.transform.SetAsLastSibling();
                var markerRect = (RectTransform)marker.transform;
                markerRect.anchoredPosition = slotPosition(slotIndex);
                markerRect.sizeDelta = new Vector2(markerSize, markerSize);
                marker.onClick.RemoveAllListeners();
                marker.onClick.AddListener(() => onPinClicked?.Invoke(slotIndex));
            }
        }

        // 형식이 틀리면 핀을 놓지 않고 이유를 보여 준다(기획 65번 §3.3).
        private void HandlePinDropped(int slotIndex)
        {
            var mask = shapeInput != null ? shapeInput.text : string.Empty;
            if (!FormationAreaShape.TryParse(mask, out var shape, out var error))
            {
                if (messageLabel != null) messageLabel.text = $"핀 모양 오류: {error}";
                return;
            }

            if (messageLabel != null) messageLabel.text = string.Empty;
            onPinDropped?.Invoke(slotIndex, shape);
        }

        private static Button CreateMarker(RectTransform parent)
        {
            var go = new GameObject("DebugPinMarker", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            go.GetComponent<Image>().color = MarkerColor;
            return go.GetComponent<Button>();
        }
    }
}
#endif
