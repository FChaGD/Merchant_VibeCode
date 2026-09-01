using Game.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Field 씬에 이동 뷰/전투 뷰/결과 팝업 하이어라키를 코드로 생성/동기화한다. 씬 YAML 수작업 편집
    /// 대신 이 도구로 재현 가능하게 만든다 - HubSceneInstaller와 동일한 방식.
    /// Hub 씬과 달리 Field 씬에는 SceneUIRoot가 아직 없어, 기존 Canvas에 이 도구가 직접 부착한다.
    /// 도착 처리(도착 팝업)는 이번 범위에 포함하지 않는다(Docs/설계/04-2026-08-25-Field씬_아키텍처.md §5.3 참고).
    /// Formation UI(정비창)의 실제 화면 요소도 이 씬에 만든다 - Hub 씬이 언로드되면 그쪽 Formation UI는
    /// 파괴되므로, Field에서 "정비창 재호출"이 동작하려면 Field 자신의 Formation UI 사본이 필요하다
    /// (FormationUIBuilder 공용, FormationPanel이 현재 로드된 콘텐츠 씬에 맞춰 다시 바인딩한다).
    /// </summary>
    public static class FieldUIInstaller
    {
        [MenuItem("Tools/Game/Build Field Scene")]
        public static void BuildFieldUI()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.Field)
            {
                Debug.LogError($"'{SceneNames.Field}' 씬이 활성 씬이어야 한다. 현재 활성 씬: '{activeScene.name}'. Field.unity를 열고 다시 실행하라.");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                Debug.LogError("씬에서 Canvas를 찾을 수 없다.");
                return;
            }

            // 그리드 드래그 등 UI 입력이 정상 동작하려면 이 씬 자신의 EventSystem이 있어야 한다 -
            // 없으면 만든다(SceneLoader가 전환 시 중복을 방지한다. EditorUIBuilder 참고).
            EditorUIBuilder.EnsureSceneEventSystem(activeScene);

            var sceneUIRoot = EditorUIBuilder.GetOrAddComponent<SceneUIRoot>(canvas.gameObject);

            var movementViewRoot = EditorUIBuilder.GetOrCreateUIObject(sceneUIRoot.transform, "MovementView");
            EditorUIBuilder.SetStretch(movementViewRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(movementViewRoot, FieldUIElementIds.MovementViewRoot);

            BuildBackground(movementViewRoot.transform);
            BuildProgressGauge(movementViewRoot.transform);
            BuildFormationButton(movementViewRoot.transform);
            BuildTacticsButton(movementViewRoot.transform);
            BuildEncounterWarning(movementViewRoot.transform);

            // BattleView/ResultPopup은 MovementView와 형제로 SceneUIRoot 바로 아래 둔다 - 결과 팝업은
            // 이동 뷰(도착)/전투 뷰(승패) 양쪽에서 모두 떠야 해서 어느 한쪽 하위에 종속시키지 않는다.
            BuildBattleView(sceneUIRoot.transform);
            // 전투 유닛 스프라이트 루트/카메라는 Canvas 밖 별도 하이어라키다(Docs/설계/13번 §2). 조립
            // 로직 자체는 EditorUIBuilder 공용 유틸리티에 있다(BattleTestSceneInstaller와 공유).
            EditorUIBuilder.EnsureBattleWorldRoot().gameObject.SetActive(false); // 평소에는 숨김 - FieldCameraController가 전환 시 활성화.
            EditorUIBuilder.ConfigureBattleCamera();
            BuildResultPopup(sceneUIRoot.transform);
            BuildTransitionCurtain(sceneUIRoot.transform);

            // 방향성 지시 UI(정비창 Formation UI와 같은 자리 - MovementView 하위가 아니라 SceneUIRoot
            // 바로 아래, 이동/전투 뷰 어느 쪽 위에서도 떠야 하므로).
            TacticsUIBuilder.Build(sceneUIRoot.transform);

            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();
            var rowPrefab = FormationUIBuilder.GetOrCreateRowPrefab();
            FormationUIBuilder.Build(sceneUIRoot.transform, slotPrefab, iconPrefab, rowPrefab);

            // 전투 뷰 유닛 프리팹도 여기서 함께 최신화한다 - ManagerHierarchyInstaller(Bootstrap)가
            // FieldUIController에 이 프리팹들을 연결할 때 재사용한다(EditorUIBuilder 공용 조립 로직).
            EditorUIBuilder.GetOrCreateBattleCharacterViewPrefab();
            EditorUIBuilder.GetOrCreateBattleProtectedViewPrefab();

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Field UI(이동 뷰/전투 뷰/결과 팝업) 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
        }

        private static void BuildBackground(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "Background");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            var image = EditorUIBuilder.EnsureImage(go, new Color(0.55f, 0.7f, 0.55f, 1f));
            image.raycastTarget = false;
            go.transform.SetAsFirstSibling(); // 배경은 항상 다른 이동 뷰 요소 뒤에 깔린다.
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.Background);
        }

        private static void BuildProgressGauge(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "ProgressGauge");
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0.15f, 0.92f), new Vector2(0.85f, 0.97f));
            var background = EditorUIBuilder.EnsureImage(root, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            background.raycastTarget = false;
            EditorUIBuilder.EnsureMarker(root, FieldUIElementIds.ProgressGauge);

            var fillGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Fill");
            EditorUIBuilder.SetStretch(fillGo.GetComponent<RectTransform>());
            var fillImage = EditorUIBuilder.EnsureImage(fillGo, new Color(0.2f, 0.5f, 0.95f, 1f));
            fillImage.sprite = EditorUIBuilder.GetOrCreateSolidSprite();
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            var gaugeView = EditorUIBuilder.GetOrAddComponent<FieldProgressGaugeView>(root);
            var so = new SerializedObject(gaugeView);
            so.FindProperty("fillImage").objectReferenceValue = fillImage;
            so.ApplyModifiedProperties();
        }

        private static void BuildFormationButton(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "FormationButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.14f, 0.10f));
            EditorUIBuilder.EnsureImage(go, new Color(0.75f, 0.87f, 1f, 1f));
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "정비창");
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.FormationButton);
        }

        // Field.FormationButton 바로 위(간격 0.02, 동일 크기)에 둔다 - 실제 좌표는 Field.unity에서
        // 실측한 값으로 계산됐다(Docs/설계/11번 §4.1).
        private static void BuildTacticsButton(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "TacticsButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.02f, 0.12f), new Vector2(0.14f, 0.20f));
            EditorUIBuilder.EnsureImage(go, new Color(0.85f, 0.75f, 0.95f, 1f));
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "방향성 지시");
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.TacticsButton);
        }

        private static void BuildEncounterWarning(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "EncounterWarning");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.3f, 0.42f), new Vector2(0.7f, 0.58f));
            var image = EditorUIBuilder.EnsureImage(go, new Color(0.85f, 0.15f, 0.15f, 0.85f));
            image.raycastTarget = false;
            EditorUIBuilder.EnsureLabel(go.transform, "인카운터 발생!");

            var canvasGroup = EditorUIBuilder.GetOrAddComponent<CanvasGroup>(go);

            var warningView = EditorUIBuilder.GetOrAddComponent<FieldEncounterWarningView>(go);
            var so = new SerializedObject(warningView);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();

            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.EncounterWarning);
            go.SetActive(false); // 평소에는 숨김 - FieldEncounterWarningView.Show() 호출 시에만 표시
        }

        // 전투 뷰 월드 오브젝트 전환(Docs/설계/13-2026-08-29-전투뷰_월드오브젝트_전환_아키텍처.md) - 실제 유닛은
        // 더 이상 이 파일이 만드는 UI 하이어라키에 없다(EditorUIBuilder.EnsureBattleWorldRoot 참고).
        // BattleView에는 배경/라벨/입력 캡처(드래그팬·휠줌)만 남는다.
        private static void BuildBattleView(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "BattleView");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.BattleViewRoot);

            // 1차 UGUI 버전(ScrollRect/Viewport/Content/AllyLayer/EnemyLayer)의 잔재를 정리한다
            // (재실행 안전성) - 실제 유닛은 이제 BattleWorldRoot 쪽에 있다.
            EditorUIBuilder.DestroyChildIfExists(go.transform, "AllyLayer");
            EditorUIBuilder.DestroyChildIfExists(go.transform, "EnemyLayer");
            EditorUIBuilder.DestroyChildIfExists(go.transform, "Viewport");
            var oldScrollRect = go.GetComponent<ScrollRect>();
            if (oldScrollRect != null)
            {
                Undo.DestroyObjectImmediate(oldScrollRect);
            }
            // 1차 UGUI 버전의 BattleFieldCameraView 컴포넌트 자체를 삭제했다 - 씬에 남은 참조는
            // 타입을 못 찾아 "Missing Script"가 된다(ManagerHierarchyInstaller.
            // RemoveMissingScriptsRecursively와 같은 이유). GetComponent로는 찾을 수 없어 이걸로 정리한다.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            // 배경은 드래그팬/휠줌 입력 캡처 대상일 뿐 시각적으로는 투명해야 한다(EditorUIBuilder.
            // CreateViewportAndContent의 Viewport와 같은 패턴 - alpha가 0에 가까워도 raycastTarget=true면
            // 클릭/드래그는 그대로 잡힌다). Screen Space Overlay 캔버스는 항상 모든 카메라 렌더링
            // "위"에 그려지므로, 여기가 불투명하면 월드 카메라가 그리는 유닛 스프라이트를 통째로
            // 가려버린다(실전투 확인 - Docs/설계/13번). "전장 밖" 여백의 어두운 배경색은 UI가 아니라
            // ConfigureBattleCamera가 Main Camera의 backgroundColor로 설정한다(스프라이트 "뒤"에
            // 그려지므로 가리지 않음).
            var background = EditorUIBuilder.EnsureImage(go, new Color(1f, 1f, 1f, 0.001f));
            background.raycastTarget = true;
            EditorUIBuilder.EnsureLabel(go.transform, "전투 중...");
            EditorUIBuilder.GetOrAddComponent<BattleFieldInputForwarder>(go);

            go.SetActive(false); // 평소에는 숨김 - FieldCameraController가 전환 시 활성화
        }

        private static void BuildResultPopup(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "ResultPopup");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.ResultPopup);

            var dim = EditorUIBuilder.EnsureImage(go, new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true; // 뒤 UI 클릭을 차단한다.

            var panelGo = EditorUIBuilder.GetOrCreateUIObject(go.transform, "Panel");
            EditorUIBuilder.SetAnchors(panelGo.GetComponent<RectTransform>(), new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.6f));
            EditorUIBuilder.EnsureImage(panelGo, new Color(0.95f, 0.95f, 0.95f, 1f));

            var messageGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "Message");
            EditorUIBuilder.SetAnchors(messageGo.GetComponent<RectTransform>(), new Vector2(0f, 0.4f), new Vector2(1f, 1f));
            var messageLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(messageGo);
            messageLabel.alignment = TextAlignmentOptions.Center;
            messageLabel.fontSize = 28;
            messageLabel.color = Color.black;
            messageLabel.raycastTarget = false;

            var buttonGo = EditorUIBuilder.GetOrCreateUIObject(panelGo.transform, "ConfirmButton");
            EditorUIBuilder.SetAnchors(buttonGo.GetComponent<RectTransform>(), new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.32f));
            EditorUIBuilder.EnsureImage(buttonGo, new Color(0.75f, 0.87f, 1f, 1f));
            var confirmButton = EditorUIBuilder.EnsureButton(buttonGo);
            var buttonLabel = EditorUIBuilder.EnsureLabel(buttonGo.transform, "확인");

            var popupView = EditorUIBuilder.GetOrAddComponent<FieldResultPopupView>(go);
            var so = new SerializedObject(popupView);
            so.FindProperty("messageLabel").objectReferenceValue = messageLabel;
            so.FindProperty("buttonLabel").objectReferenceValue = buttonLabel;
            so.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            so.ApplyModifiedProperties();

            go.SetActive(false); // 평소에는 숨김 - FieldResultPopupView.Show() 호출 시에만 표시
        }

        // sceneUIRoot의 마지막 자식으로 붙여 항상 최상단에 그려지게 한다 - MovementView/BattleView가
        // 슬라이드 중인 어느 위치에서도 뷰포트 전체를 가려야 한다(FieldTransitionCurtainView 참고).
        private static void BuildTransitionCurtain(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "TransitionCurtain");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(go, FieldUIElementIds.TransitionCurtain);

            var image = EditorUIBuilder.EnsureImage(go, Color.black);
            image.raycastTarget = true; // 전환 중 뒤쪽 UI 입력을 차단한다.

            var canvasGroup = EditorUIBuilder.GetOrAddComponent<CanvasGroup>(go); // 페이드 아웃(알파 조절)에 필요.

            var curtainView = EditorUIBuilder.GetOrAddComponent<FieldTransitionCurtainView>(go);
            var so = new SerializedObject(curtainView);
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedProperties();

            go.transform.SetAsLastSibling();
            go.SetActive(false); // 평소에는 숨김 - FieldTransitionCurtainView.Show() 호출 시에만 표시
        }
    }
}
