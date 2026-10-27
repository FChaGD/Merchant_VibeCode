using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 인벤토리 상시 호출 버튼 조립(Docs/Refactor/2026-10-26_씬전용.md H-3) - 버튼 순서는 인벤토리 팝업 스펙 목록 순서(공통 D-2).
    /// </summary>
    internal static class InventoryShortcutUIBuilder
    {
        // ==================== 인벤토리 상시 호출 버튼 ====================
        // 상행 준비 버튼(우하단)과 대칭 위치(우상단에서 같은 비율 거리)에 큰 버튼 1개(상단 물류품) + 그 아래
        // 작은 버튼 3개를 좌우로 둔다(사용자 확정, 2026-09-24). 큰 버튼은 상행 준비 버튼과 같은 크기,
        // 작은 버튼은 폭을 3등분하고 높이는 상단 배치/방향성 지시와 같은 가로세로 비율로 맞춘다 - 묶음 전체가
        // 상행 준비 버튼의 좌우 폭을 넘지 않는다. 상행 준비 버튼 앵커에서 매번 계산하므로 그 버튼을 옮기고
        // 재실행하면 따라온다. PersistentLayer라 depth 전환과 무관하게 보이지만, 모달 팝업(상행 준비/상단 배치/
        // 방향성 지시)이 열려 있는 동안은 숨는다(사용자 확정, 2026-09-24 - Docs/설계/38번 §1-3).
        private static readonly Color InventoryShortcutButtonColor = new(0.8f, 0.85f, 0.95f, 1f);
        // 인벤토리 버튼 안에서 키 글자 열이 시작하는 가로 비율(설계 99번 §5.1).
        private const float KeyLabelColumnMin = 0.8f;

        public static void Build(SceneUIRoot sceneUIRoot, Transform persistentLayer)
        {
            var root = EditorUIBuilder.EnsureStretchLayer(persistentLayer, "InventoryShortcuts", HubUIElementIds.InventoryShortcutRoot);

            var departure = EditorUIBuilder.FindMarker(sceneUIRoot, HubUIElementIds.DepartureButton);
            if (departure == null)
            {
                Debug.LogWarning($"Hub UI에서 '{HubUIElementIds.DepartureButton}' 요소를 찾을 수 없어 인벤토리 버튼 배치를 건너뛴다.");
                return;
            }

            var departureRect = (RectTransform)departure.transform;
            var leftX = departureRect.anchorMin.x;
            var width = departureRect.anchorMax.x - leftX;
            var bigHeight = departureRect.anchorMax.y - departureRect.anchorMin.y;
            // 상행 준비 버튼의 아래쪽 여백(anchorMin.y)을 위쪽 여백으로 그대로 쓴다.
            var bigTop = 1f - departureRect.anchorMin.y;
            var bigBottom = bigTop - bigHeight;

            var smallWidth = (width - EditorHubLayout.HubActionButtonGap * 2f) / 3f;
            var smallHeight = EditorHubLayout.HubActionButtonBaseHeight * (smallWidth / EditorHubLayout.HubActionButtonBaseWidth);
            var smallTop = bigBottom - EditorHubLayout.HubActionButtonGap;

            var popupSpecs = InventoryPopupSpecs.All; // 표시 순서는 스펙 목록 순서(Docs/Refactor/2026-10-26_공통.md D-2)
            var big = BuildInventoryShortcutButton(root, popupSpecs[0].PopupId, "상단 물류품", minFontSize: 18f);
            EditorUIBuilder.SetAnchors(big, new Vector2(leftX, bigBottom), new Vector2(leftX + width, bigTop));

            var smallLabels = new[] { "전투 장비", "소모품", "개인 물품" };
            for (var i = 0; i < smallLabels.Length; i++)
            {
                // 작은 버튼은 1080p 기준 약 100x48px라 기본 최소 글자 크기(18)로는 라벨이 넘친다 - 최소치만 낮춘다.
                var small = BuildInventoryShortcutButton(root, popupSpecs[i + 1].PopupId, smallLabels[i], minFontSize: 10f);
                var x = leftX + i * (smallWidth + EditorHubLayout.HubActionButtonGap);
                EditorUIBuilder.SetAnchors(small, new Vector2(x, smallTop - smallHeight), new Vector2(x + smallWidth, smallTop));
            }
        }

        private static RectTransform BuildInventoryShortcutButton(Transform parent, string popupId, string label, float minFontSize)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, $"Inventory_{popupId}");
            EditorUIBuilder.EnsureImage(go, InventoryShortcutButtonColor);
            EditorUIBuilder.EnsureButton(go);
            var nameLabel = EditorUIBuilder.EnsureLabel(go.transform, label, autoSize: true, minFontSize: minFontSize, maxFontSize: 30f);
            EditorUIBuilder.EnsureMarker(go, HubUIElementIds.InventoryShortcutButton(popupId));

            // 키 글자(설계 99번 §5.1) - 바인딩 표시 문자열은 런타임에 InputManager가 채운다. 클릭은 버튼이 받도록 레이캐스트를 끈다.
            // 작은 버튼(720p 약 67x32px)은 이름 글자가 폭을 다 채워 모서리에 겹쳐 놓을 자리가 없다(2026-10-21 실측) -
            // 오른쪽 열을 키 글자 전용으로 떼고 이름 글자는 왼쪽 영역에서 자동 축소되게 한다.
            EditorUIBuilder.SetAnchors(nameLabel.rectTransform, Vector2.zero, new Vector2(KeyLabelColumnMin, 1f));
            // 좁아진 폭에서 두 줄로 꺾이면 32px 높이를 꽉 채운다 - 한 줄 유지, 자동 축소로만 맞춘다.
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var keyLabel = EditorUIBuilder.EnsureTextBlock(go.transform, "KeyLabel", new Vector2(KeyLabelColumnMin, 0.05f), new Vector2(0.97f, 0.95f), 16f, TextAlignmentOptions.TopRight, Color.black);
            keyLabel.text = string.Empty;
            keyLabel.raycastTarget = false;
            EditorUIBuilder.EnsureMarker(keyLabel.gameObject, HubUIElementIds.InventoryShortcutKeyLabel(popupId));
            return go.GetComponent<RectTransform>();
        }
    }
}
