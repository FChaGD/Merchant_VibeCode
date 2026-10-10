using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 골드 변환 모달(설계 83번 §6.2)을 get-or-create로 조립한다. 시설 화면 빌더들과 같은 이유로 HubSceneInstaller 내부 메서드로 두지 않는다.
    /// 저수준 조립은 EditorUIBuilder에 위임한다. 앵커 수치는 잠정값이다 - 사용자 실전 확인 후 조정한다.
    /// </summary>
    internal static class GoldConversionUIBuilder
    {
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.5f);
        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color ConvertColor = new(0.75f, 0.87f, 1f, 1f);

        public static void Build(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "GoldConversion");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(root, DimColor).raycastTarget = true; // 뒤 UI 클릭 차단
            EditorUIBuilder.EnsureMarker(root, GoldConversionUIElementIds.Root);

            var panel = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Panel");
            EditorUIBuilder.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.78f));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            var title = EditorUIBuilder.GetOrCreateUIObject(panel.transform, "Title");
            EditorUIBuilder.SetAnchors(title.GetComponent<RectTransform>(), new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            EditorUIBuilder.EnsureLabel(title.transform, "골드 상자 변환", autoSize: true, minFontSize: 14f, maxFontSize: 28f);

            var info = EditorUIBuilder.GetOrCreateUIObject(panel.transform, "Info");
            EditorUIBuilder.SetAnchors(info.GetComponent<RectTransform>(), new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.86f));
            var infoLabel = EditorUIBuilder.EnsureLabel(info.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 22f);
            infoLabel.alignment = TextAlignmentOptions.TopLeft;
            EditorUIBuilder.EnsureMarker(infoLabel.gameObject, GoldConversionUIElementIds.InfoText);

            BuildButton(panel.transform, "DecreaseButton", "▼", new Vector2(0.08f, 0.28f), new Vector2(0.22f, 0.38f), ButtonColor, GoldConversionUIElementIds.DecreaseButton);
            var count = EditorUIBuilder.GetOrCreateUIObject(panel.transform, "Count");
            EditorUIBuilder.SetAnchors(count.GetComponent<RectTransform>(), new Vector2(0.24f, 0.28f), new Vector2(0.62f, 0.38f));
            var countLabel = EditorUIBuilder.EnsureLabel(count.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 22f);
            EditorUIBuilder.EnsureMarker(countLabel.gameObject, GoldConversionUIElementIds.CountText);
            BuildButton(panel.transform, "IncreaseButton", "▲", new Vector2(0.64f, 0.28f), new Vector2(0.78f, 0.38f), ButtonColor, GoldConversionUIElementIds.IncreaseButton);
            BuildButton(panel.transform, "ExcessAllButton", "초과분 전부", new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.25f), ButtonColor, GoldConversionUIElementIds.ExcessAllButton);
            BuildButton(panel.transform, "ConvertButton", "변환", new Vector2(0.08f, 0.03f), new Vector2(0.48f, 0.13f), ConvertColor, GoldConversionUIElementIds.ConvertButton);
            BuildButton(panel.transform, "CloseButton", "닫기", new Vector2(0.52f, 0.03f), new Vector2(0.92f, 0.13f), ButtonColor, GoldConversionUIElementIds.CloseButton);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static void BuildButton(Transform parent, string objectName, string text, Vector2 anchorMin, Vector2 anchorMax, Color color, string markerId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, objectName);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, color);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, text, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            EditorUIBuilder.EnsureMarker(go, markerId);
        }
    }
}
