using System;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Editor
{
    /// <summary>
    /// 매니저 하이어라키를 영속 Bootstrap 씬으로 옮기고, 콘텐츠 씬(본부/상행)을 준비하며,
    /// Build Settings에 등록하는 씬 구조 셋업 도구. 씬 파일 수작업 편집 대신 재현 가능한 절차로 대체한다.
    /// </summary>
    public static class BootstrapSceneInstaller
    {
        private const string ManagersRootName = "Managers";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        // ContentSceneId가 유일한 콘텐츠 씬 식별자 출처다 - enum에 값을 추가하면 이 목록도 자동으로
        // 늘어나 따로 배열을 유지할 필요가 없다(Docs/Refactor/공통_점검.md 3단계 수정안).
        private static string[] ContentScenePaths =>
            Enum.GetValues(typeof(ContentSceneId))
                .Cast<ContentSceneId>()
                .Select(id => $"Assets/Scenes/{id}.unity")
                .ToArray();

        [MenuItem("Tools/Game/Setup/1. Migrate Managers To Bootstrap Scene")]
        public static void MigrateManagersToBootstrapScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var managersRoot = activeScene.GetRootGameObjects().FirstOrDefault(go => go.name == ManagersRootName);

            if (managersRoot == null)
            {
                Debug.LogWarning($"'{activeScene.name}' 씬에서 '{ManagersRootName}' 루트를 찾을 수 없다. 먼저 Tools/Game/Build Manager Hierarchy를 실행하라.");
                return;
            }

            var bootstrapScene = File.Exists(BootstrapScenePath)
                ? EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            SceneManager.MoveGameObjectToScene(managersRoot, bootstrapScene);

            if (managersRoot.GetComponent<BootstrapRoot>() == null)
            {
                Undo.AddComponent<BootstrapRoot>(managersRoot);
            }

            EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath);
            EditorSceneManager.SaveScene(activeScene);
            EditorSceneManager.CloseScene(bootstrapScene, true);

            Debug.Log($"'{ManagersRootName}' 하이어라키를 {BootstrapScenePath}로 이전했다. '{activeScene.name}'은 콘텐츠 전용 씬으로 남는다.");
        }

        [MenuItem("Tools/Game/Setup/2. Create Content Scenes (Hub, Field)")]
        public static void CreateContentScenes()
        {
            foreach (var path in ContentScenePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"{path}은 이미 존재한다. 건너뛴다.");
                    continue;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
                Debug.Log($"{path} 생성 완료.");
            }
        }

        [MenuItem("Tools/Game/Setup/3. Register Scenes In Build Settings")]
        public static void RegisterScenesInBuildSettings()
        {
            // Bootstrap이 항상 0번 인덱스(최초 로드 씬)가 되도록 매번 순서를 재구성한다.
            var orderedPaths = new[] { BootstrapScenePath }.Concat(ContentScenePaths).ToArray();

            var newSceneList = orderedPaths
                .Where(path =>
                {
                    var exists = File.Exists(path);
                    if (!exists)
                    {
                        Debug.LogWarning($"{path}이 존재하지 않아 Build Settings에 등록하지 못했다.");
                    }

                    return exists;
                })
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToList();

            // Bootstrap/콘텐츠 씬이 아닌 기존 등록 씬(예: SampleScene)은 순서 뒤쪽에 그대로 유지한다.
            var otherScenes = EditorBuildSettings.scenes.Where(s => !orderedPaths.Contains(s.path));
            newSceneList.AddRange(otherScenes);

            EditorBuildSettings.scenes = newSceneList.ToArray();

            // EditorBuildSettings.scenes 대입만으로는 ProjectSettings/EditorBuildSettings.asset에
            // 즉시 기록되지 않는 경우가 있어 명시적으로 저장을 강제한다.
            AssetDatabase.SaveAssets();
            Debug.Log("Build Settings 씬 목록 갱신 완료 (Bootstrap이 0번 인덱스).");
        }
    }
}
