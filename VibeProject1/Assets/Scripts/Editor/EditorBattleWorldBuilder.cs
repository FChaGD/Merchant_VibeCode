using Game.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 전투 뷰(월드 오브젝트) 공용 조립 - 유닛 스프라이트 루트·전투 카메라·전투 유닛 프리팹. FieldUIInstaller(Field 씬)와
    /// BattleTestSceneInstaller(독립 배틀 테스트 씬)가 함께 쓴다. UI가 아니라 전투 월드 조립이라 UI 조립 공용 도구(EditorUIBuilder)에서
    /// 떼어 냈다(Docs/Refactor/2026-10-26_공통.md C-4) - 두 인스톨러가 서로의 내부 메서드를 참조하지 않게 하는 이유는 그대로다(CLAUDE.md 씬 편집 컨벤션).
    /// </summary>
    internal static class EditorBattleWorldBuilder
    {
        private const string BattleWorldRootName = "BattleWorldRoot";
        private static readonly string BattleLayerName = BattleFieldGeometry.BattleLayerName;
        private const string BattlePrefabFolder = "Assets/Prefabs/UI/Battle";
        private const string BattleCharacterViewPrefabPath = BattlePrefabFolder + "/BattleCharacterUnitView.prefab";
        private const string BattleProtectedViewPrefabPath = BattlePrefabFolder + "/BattleProtectedUnitView.prefab";
        private const string BattlePendingReinforcementViewPrefabPath = BattlePrefabFolder + "/BattlePendingReinforcementView.prefab";

        /// <summary>
        /// 전투 유닛(캐릭터/보호목표) 스프라이트의 루트 - Canvas 밖 씬 루트에 독립적으로 만든다
        /// (Docs/설계/13번 §2, UI 좌표계와 섞이면 스케일 문제가 재발한다). 활성/비활성 여부는 호출자가
        /// 결정한다 - Field는 카메라 전환 전까지 숨겨야 하고, 배틀 테스트 씬은 처음부터 항상 보여야 한다.
        /// </summary>
        public static BattleWorldRoot EnsureBattleWorldRoot()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            GameObject root = null;
            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                if (rootObject.name == BattleWorldRootName)
                {
                    root = rootObject;
                    break;
                }
            }
            if (root == null)
            {
                root = new GameObject(BattleWorldRootName);
                Undo.RegisterCreatedObjectUndo(root, $"Create {BattleWorldRootName}");
            }
            var battleWorldRoot = EditorUIBuilder.GetOrAddComponent<BattleWorldRoot>(root);

            var battleLayer = LayerMask.NameToLayer(BattleLayerName);
            if (battleLayer < 0)
            {
                Debug.LogWarning($"'{BattleLayerName}' 레이어가 없다 - Project Settings > Tags and Layers에서 추가하라. 추가 전까지는 Default 레이어로 대체된다.");
                battleLayer = 0;
            }

            EnsureBattleUnitLayer(root.transform, BattleWorldLayerNames.Ally, battleLayer);
            EnsureBattleUnitLayer(root.transform, BattleWorldLayerNames.Enemy, battleLayer);
            EditorUIBuilder.GetOrAddComponent<BattleBackgroundGridView>(root);

            return battleWorldRoot;
        }

        // 유닛 스폰 부모 - 일반 Transform(RectTransform 아님)이라 UI 좌표계와 무관하게 순수 월드
        // 좌표로 배치된다. layer를 Battle로 지정해 전투 카메라의 cullingMask와 맞춘다.
        private static void EnsureBattleUnitLayer(Transform parent, string name, int layer)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            }
            go.layer = layer;
            go.transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 새 카메라를 만들지 않고 씬의 기존 Main Camera를 재사용한다(Docs/설계/13번 §6 확정 - 이미
        /// Orthographic이고 AudioListener도 있어 재사용이 더 안전함).
        /// </summary>
        public static void ConfigureBattleCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("씬에서 Main Camera를 찾을 수 없어 전투 카메라를 구성하지 못했다.");
                return;
            }

            var battleLayer = LayerMask.NameToLayer(BattleLayerName);
            if (battleLayer >= 0)
            {
                mainCamera.cullingMask = 1 << battleLayer;
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);

            EditorUIBuilder.GetOrAddComponent<BattleFieldWorldCameraView>(mainCamera.gameObject);
        }

        private static void EnsureBattlePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }
            if (!AssetDatabase.IsValidFolder(BattlePrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Battle");
            }
        }

        /// <summary>
        /// ManagerHierarchyInstaller(Bootstrap)/BattleTestSceneInstaller가 전투 뷰 컨트롤러에 연결할 때
        /// 재사용한다. 1차 UGUI 버전(RectTransform+Image) 프리팹이 이미 그 경로에 있으면 SpriteRenderer
        /// 버전으로 재생성한다(재실행 안전성 - 존재 여부만으론 옛 버전인지 구분이 안 돼 SpriteRenderer
        /// 보유 여부로 판정한다).
        /// </summary>
        public static BattleCharacterUnitView GetOrCreateBattleCharacterViewPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BattleCharacterViewPrefabPath);
            // 체력 게이지바(사용자 요청) 추가 전 프리팹은 게이지 자식이 아예 없거나, 배경/채움 스케일이
            // 서로 간섭하던 구버전 구조일 수 있다(HasUpToDateHealthGauge 참고) - 그 경우도
            // UGUI→SpriteRenderer 전환 때와 같은 방식으로 통째로 재생성한다.
            if (existing != null && HasUpToDateHealthGauge(existing))
            {
                return existing.GetComponent<BattleCharacterUnitView>();
            }

            EnsureBattlePrefabFolder();

            var go = new GameObject("BattleCharacterUnitView", typeof(SpriteRenderer));
            var charLayer = LayerMask.NameToLayer(BattleLayerName);
            go.layer = charLayer >= 0 ? charLayer : 0;
            var renderer = go.GetComponent<SpriteRenderer>();
            var gaugeView = CreateHealthGaugeChild(go.transform, charLayer);

            var view = go.AddComponent<BattleCharacterUnitView>();
            var so = new SerializedObject(view);
            so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            so.FindProperty("gaugeView").objectReferenceValue = gaugeView;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, BattleCharacterViewPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<BattleCharacterUnitView>();
        }

        public static BattleProtectedUnitView GetOrCreateBattleProtectedViewPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BattleProtectedViewPrefabPath);
            if (existing != null && HasUpToDateHealthGauge(existing))
            {
                return existing.GetComponent<BattleProtectedUnitView>();
            }

            EnsureBattlePrefabFolder();

            var go = new GameObject("BattleProtectedUnitView", typeof(SpriteRenderer));
            var protLayer = LayerMask.NameToLayer(BattleLayerName);
            go.layer = protLayer >= 0 ? protLayer : 0;
            var renderer = go.GetComponent<SpriteRenderer>();
            var gaugeView = CreateHealthGaugeChild(go.transform, protLayer);

            var view = go.AddComponent<BattleProtectedUnitView>();
            var so = new SerializedObject(view);
            so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            so.FindProperty("gaugeView").objectReferenceValue = gaugeView;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, BattleProtectedViewPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<BattleProtectedUnitView>();
        }

        // 전투 중 신규 소환 유령(설계 25번 §6.3, 사용자 확정 2026-09-05) - 본체 SpriteRenderer +
        // 잔여시간 월드 텍스트(TextMeshPro, UGUI 아님 - 이 프리팹은 BattleWorldRoot 산하 순수 월드
        // 오브젝트라 Canvas가 없다) 구성.
        public static BattlePendingReinforcementView GetOrCreateBattlePendingReinforcementViewPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BattlePendingReinforcementViewPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<BattlePendingReinforcementView>();
            }

            EnsureBattlePrefabFolder();

            var go = new GameObject("BattlePendingReinforcementView", typeof(SpriteRenderer));
            var layer = LayerMask.NameToLayer(BattleLayerName);
            go.layer = layer >= 0 ? layer : 0;
            var renderer = go.GetComponent<SpriteRenderer>();

            var textGo = new GameObject("RemainingSecondsText", typeof(TextMeshPro));
            textGo.transform.SetParent(go.transform, false);
            textGo.layer = layer >= 0 ? layer : 0;
            textGo.transform.localPosition = new Vector3(0f, 0.5f, 0f); // 본체 위쪽에 표시
            var text = textGo.GetComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 3f;
            text.color = Color.white;

            var view = go.AddComponent<BattlePendingReinforcementView>();
            var so = new SerializedObject(view);
            so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            so.FindProperty("remainingSecondsText").objectReferenceValue = text;
            so.ApplyModifiedProperties();

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, BattlePendingReinforcementViewPrefabPath);
            Object.DestroyImmediate(go);

            return savedPrefab.GetComponent<BattlePendingReinforcementView>();
        }

        // 게이지 컴포넌트가 있어도 구버전(배경 SpriteRenderer가 게이지 오브젝트 자신에 있어 채움과
        // 스케일이 간섭하던 구조, CreateHealthGaugeChild 주석 참고)일 수 있다 - 새 구조는 게이지
        // 오브젝트 자신엔 SpriteRenderer가 없다(배경/채움 둘 다 그 자식으로 분리됨).
        private static bool HasUpToDateHealthGauge(GameObject prefabRoot)
        {
            var gaugeView = prefabRoot.GetComponentInChildren<BattleHealthGaugeView>();
            return gaugeView != null && gaugeView.GetComponent<SpriteRenderer>() == null;
        }

        // 체력 게이지바(BattleHealthGaugeView, 사용자 요청) 자식을 만들어 부착한다 - 캐릭터/보호대상
        // 프리팹 둘 다 같은 구조(배경+채움 SpriteRenderer 2개)를 쓴다. 배경/채움을 "HealthGauge"의
        // 형제(sibling) 자식으로 각각 따로 둔다 - 배경 SpriteRenderer를 BattleHealthGaugeView가 붙은
        // 오브젝트 자신에 두면(예전 구조) 배경 크기 조정용 localScale이 곧 부모 스케일이 되어 자식인
        // 채움 바에도 그대로 곱해져 이중으로 축소되는 버그가 있었다(실전투 확인됨) - "HealthGauge"는
        // 순수 위치 앵커로만 쓰고 스케일은 절대 건드리지 않는다.
        private static BattleHealthGaugeView CreateHealthGaugeChild(Transform parent, int layer)
        {
            var gaugeGo = new GameObject("HealthGauge");
            gaugeGo.transform.SetParent(parent, false);
            gaugeGo.layer = layer >= 0 ? layer : 0;

            var backgroundGo = new GameObject("Background", typeof(SpriteRenderer));
            backgroundGo.transform.SetParent(gaugeGo.transform, false);
            backgroundGo.layer = layer >= 0 ? layer : 0;
            var backgroundRenderer = backgroundGo.GetComponent<SpriteRenderer>();

            var fillGo = new GameObject("Fill", typeof(SpriteRenderer));
            fillGo.transform.SetParent(gaugeGo.transform, false);
            fillGo.layer = layer >= 0 ? layer : 0;
            var fillRenderer = fillGo.GetComponent<SpriteRenderer>();

            var gaugeView = gaugeGo.AddComponent<BattleHealthGaugeView>();
            var so = new SerializedObject(gaugeView);
            so.FindProperty("backgroundRenderer").objectReferenceValue = backgroundRenderer;
            so.FindProperty("fillRenderer").objectReferenceValue = fillRenderer;
            so.ApplyModifiedProperties();

            return gaugeView;
        }
    }
}
