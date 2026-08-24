using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Field 씬에 이동 뷰(배경/진행 게이지/정비창 재호출 버튼) 하이어라키를 코드로 생성/동기화한다.
    /// 씬 YAML 수작업 편집 대신 이 도구로 재현 가능하게 만든다 - FormationUIInstaller/TripUIInstaller와
    /// 동일한 방식. Hub 씬과 달리 Field 씬에는 SceneUIRoot가 아직 없어, 기존 Canvas에 이 도구가 직접
    /// 부착한다. 전투 뷰/카메라 전환/팝업은 이번 범위에 포함하지 않는다(Docs/설계/04_Field씬_아키텍처.md
    /// 참고, 후속 세션에서 추가 예정).
    /// Formation UI(정비창)의 실제 화면 요소도 이 씬에 만든다 - Hub 씬이 언로드되면 그쪽 Formation UI는
    /// 파괴되므로, Field에서 "정비창 재호출"이 동작하려면 Field 자신의 Formation UI 사본이 필요하다
    /// (FormationUIBuilder 공용, FormationPanel이 현재 로드된 콘텐츠 씬에 맞춰 다시 바인딩한다).
    /// </summary>
    public static class FieldUIInstaller
    {
        [MenuItem("Tools/Game/Build Field UI")]
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

            FormationUIBuilder.EnsurePrefabFolder();
            var slotPrefab = FormationUIBuilder.GetOrCreateSlotPrefab();
            var iconPrefab = FormationUIBuilder.GetOrCreateIconPrefab();
            FormationUIBuilder.Build(sceneUIRoot, slotPrefab, iconPrefab);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("Field UI(이동 뷰) 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
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
    }
}
