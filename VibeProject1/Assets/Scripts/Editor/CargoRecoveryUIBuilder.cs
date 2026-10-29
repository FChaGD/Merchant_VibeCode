using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 회수 적재 패널 조립 - Field(전투 후 회수·이벤트 무역품 정리)와 Hub(휴식 이벤트 무역품 정리, 설계 109 §6.1)가 같은 구조를 씬마다 하나씩 둔다.
    /// 한 인스톨러의 내부 메서드를 다른 인스톨러가 쓰지 않도록 공용 빌더로 뺐다. 마커 Id는 Field 접두 문자열이지만 씬이 달라 충돌하지 않는다.
    /// </summary>
    internal static class CargoRecoveryUIBuilder
    {
        /// <summary>
        /// 승리 후 회수 물품 적재 패널(Docs/설계/79번 §7). 무역품 구매 화면과 같은 편집 본문(InventoryPopupUIBuilder.BuildArrangementBody)을 쓴다.
        /// 패널 루트 마커(FieldCargoRecoveryUIElementIds.Root)와 편집 본문 루트 마커(InventoryPopupUIElementIds.Root(InventoryPrefix))의
        /// 문자열이 같아("Field.CargoRecovery.Root") 두 루트를 같은 오브젝트로 둔다 - 오브젝트를 나누면 같은 Id 마커가 둘이 되어
        /// SceneUIRoot가 하나만 수집한다. 편집 본문은 루트 윗부분(0.9 이상)을 제목 줄로 비워 두므로 제목과 [완료]는 그 줄에 둔다.
        /// placeAfter(Field = 결과 팝업) 바로 뒤 형제로 두어 그보다 위에 그리고, 확인 대화상자는 패널 자식의 마지막 형제로 패널 위에 그린다.
        /// </summary>
        public static Transform Build(Transform parent, Transform placeAfter)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "CargoRecoveryPanel");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(rootRect, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            EditorUIBuilder.EnsureImage(root, new Color(0.96f, 0.95f, 0.92f, 1f));

            InventoryPopupUIBuilder.BuildArrangementBody(rootRect, FieldCargoRecoveryUIElementIds.InventoryPrefix, hasStaging: true, hasSections: true);
            // BuildArrangementBody가 붙인 본문 루트 마커와 같은 문자열이지만, 의미상 패널 루트 Id로 한 번 더 명시한다(요약 주석 참고).
            EditorUIBuilder.EnsureMarker(root, FieldCargoRecoveryUIElementIds.Root);

            var header = EditorUIBuilder.GetOrCreateUIObject(rootRect, "Header");
            EditorUIBuilder.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), new Vector2(0.8f, 1f));
            EditorUIBuilder.EnsureImage(header, new Color(0.72f, 0.66f, 0.55f, 1f)).raycastTarget = false;
            EditorUIBuilder.EnsureLabel(header.transform, "회수 물품 적재", autoSize: true, minFontSize: 12f, maxFontSize: 26f);

            var done = EditorUIBuilder.GetOrCreateUIObject(rootRect, "DoneButton");
            EditorUIBuilder.SetAnchors(done.GetComponent<RectTransform>(), new Vector2(0.82f, 0.91f), new Vector2(0.98f, 0.99f));
            EditorUIBuilder.EnsureImage(done, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(done);
            EditorUIBuilder.EnsureLabel(done.transform, "완료", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(done, FieldCargoRecoveryUIElementIds.DoneButton);

            // 드래그 레이어는 제목 줄·[완료]보다 위에 있어야 끌던 물품이 그 아래로 숨지 않는다. 본문 조립(BuildArrangementBody)이 드래그 레이어를
            // 맨 뒤로 보낸 "뒤에" 제목·[완료]를 처음 만들면 첫 실행과 재실행의 순서가 달라지므로, 여기서 다시 맨 뒤로 보낸다(2026-10-05 검진 지적 7).
            var dragLayer = rootRect.Find("DragLayer");
            if (dragLayer != null) dragLayer.SetAsLastSibling();
            // 버리기 경고(설계 102번 §5.1)도 제목 줄·[완료]를 덮어야 한다 - 드래그 레이어 뒤, 완료 확인 대화상자 앞.
            var discardDialog = rootRect.Find("DiscardDialog");
            if (discardDialog != null) discardDialog.SetAsLastSibling();

            // 드래그 레이어보다도 위(마지막 형제)에 둔다 - BuildConfirmDialog가 SetAsLastSibling한다.
            EditorUIBuilder.BuildConfirmDialog(rootRect, "ConfirmDialog", FieldCargoRecoveryUIElementIds.ConfirmDialog);

            if (placeAfter != null) EditorUIBuilder.PlaceSiblingAfter(root.transform, placeAfter);
            root.SetActive(false); // 평소에는 숨김 - 회수 적재 단계에서 UIManager가 연다.
            return root.transform;
        }
    }
}
