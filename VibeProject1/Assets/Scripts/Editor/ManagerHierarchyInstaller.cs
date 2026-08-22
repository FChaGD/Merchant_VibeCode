using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 매니저 8개(DependencyManager, SceneLoader 포함) 하이어라키를 코드로 생성/동기화한다.
    /// 씬 YAML 수작업 편집 대신 이 도구로 재현 가능하게 만든다. 이미 존재하는 오브젝트는 재사용하며
    /// DependencyManager의 managedComponents 목록만 항상 최신 상태로 재동기화한다.
    /// </summary>
    public static class ManagerHierarchyInstaller
    {
        private const string RootName = "Managers";

        [MenuItem("Tools/Game/Build Manager Hierarchy")]
        public static void BuildManagerHierarchy()
        {
            var root = GetOrCreateRoot();

            var dependencyManager = GetOrCreateManager<DependencyManager>(root.transform, "DependencyManager");
            var gameManager = GetOrCreateManager<GameManager>(root.transform, "GameManager");
            var inputManager = GetOrCreateManager<InputManager>(root.transform, "InputManager");
            var uiManager = GetOrCreateManager<UIManager>(root.transform, "UIManager");
            var battleManager = GetOrCreateManager<BattleManager>(root.transform, "BattleManager");
            var aiManager = GetOrCreateManager<AIManager>(root.transform, "AIManager");
            var encounterManager = GetOrCreateManager<EncounterManager>(root.transform, "EncounterManager");

            // SceneLoader는 GameManager와 같은 GameObject에 부착하되, 자체적으로 DI에 등록되는
            // 독립된 관리 대상이므로 managedComponents 동기화 목록에도 포함한다.
            var sceneLoader = EnsureSiblingComponent<SceneLoader>(gameManager.gameObject);

            // HubUIController/FormationPanel은 전역 매니저가 아니라 UIManager 산하 컴포넌트이므로 같은 GameObject에 부착한다.
            EnsureSiblingComponent<HubUIController>(uiManager.gameObject);
            EnsureSiblingComponent<FormationPanel>(uiManager.gameObject);

            SyncManagedComponents(dependencyManager, new MonoBehaviour[]
            {
                gameManager,
                inputManager,
                uiManager,
                battleManager,
                aiManager,
                encounterManager,
                sceneLoader,
            });

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("매니저 하이어라키 생성/동기화 완료. 씬을 저장(Ctrl+S)해야 변경사항이 파일에 반영된다.");
        }

        private static GameObject GetOrCreateRoot()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                if (rootObject.name == RootName)
                {
                    return rootObject;
                }
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
            return root;
        }

        private static T GetOrCreateManager<T>(Transform parent, string objectName) where T : Component
        {
            var existing = parent.Find(objectName);
            if (existing != null)
            {
                var component = existing.GetComponent<T>();
                return component != null ? component : Undo.AddComponent<T>(existing.gameObject);
            }

            var go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {objectName}");
            return Undo.AddComponent<T>(go);
        }

        private static T EnsureSiblingComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static void SyncManagedComponents(DependencyManager dependencyManager, MonoBehaviour[] managedComponents)
        {
            var serializedDependencyManager = new SerializedObject(dependencyManager);
            var managedComponentsProperty = serializedDependencyManager.FindProperty("managedComponents");

            managedComponentsProperty.arraySize = managedComponents.Length;
            for (var i = 0; i < managedComponents.Length; i++)
            {
                managedComponentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = managedComponents[i];
            }

            serializedDependencyManager.ApplyModifiedProperties();
        }
    }
}
