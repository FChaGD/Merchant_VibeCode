using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 전투 디버그 기즈모 3종(포위망/방진선/이동 목적지)을 BattleManager와 같은 GameObject에
    /// 설치/제거한다. ManagerHierarchyInstaller(전체 매니저 하이어라키 빌드)와 분리해 여기 둔 이유는
    /// DebugBootstrapReentryGuardInstaller와 같다 - "게임 빌드"와 "디버그 도구 켜고 끄기"는 다른
    /// 관심사라, 기즈모만 껐다 켰다 하고 싶을 때 전체 매니저 하이어라키를 다시 빌드할 필요가 없게
    /// 한다. BattleManager는 Bootstrap 씬 하나에만 있어(콘텐츠 씬마다 도는
    /// DebugBootstrapReentryGuardInstaller와 달리) 씬을 열고 닫는 루프 없이 현재 활성 씬에서 바로
    /// 찾는다 - Bootstrap.unity를 열고 실행해야 한다.
    /// 걷어낼 때는 이 파일과 BattleSurroundGizmoView/BattleFrontlineGizmoView/
    /// BattleMoveTargetGizmoView.cs(+.meta 전부)만 지우고 Remove 메뉴를 한 번 실행하면 된다.
    /// </summary>
    public static class BattleGizmoInstaller
    {
        [MenuItem("Tools/Game/Debug/Install Battle Gizmos")]
        public static void InstallGizmos()
        {
            var battleManager = Object.FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
            if (battleManager == null)
            {
                Debug.LogWarning("씬에서 BattleManager를 찾을 수 없다 - Bootstrap.unity를 열고 다시 실행하라.");
                return;
            }

            EnsureComponent<BattleSurroundGizmoView>(battleManager.gameObject);
            EnsureComponent<BattleFrontlineGizmoView>(battleManager.gameObject);
            EnsureComponent<BattleMoveTargetGizmoView>(battleManager.gameObject);

            EditorSceneManager.MarkSceneDirty(battleManager.gameObject.scene);
            Debug.Log("전투 디버그 기즈모 설치/동기화 완료. Ctrl+S로 씬을 저장했다.");
        }

        [MenuItem("Tools/Game/Debug/Remove Battle Gizmos")]
        public static void RemoveGizmos()
        {
            var battleManager = Object.FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
            if (battleManager == null)
            {
                return;
            }

            RemoveComponent<BattleSurroundGizmoView>(battleManager.gameObject);
            RemoveComponent<BattleFrontlineGizmoView>(battleManager.gameObject);
            RemoveComponent<BattleMoveTargetGizmoView>(battleManager.gameObject);

            EditorSceneManager.MarkSceneDirty(battleManager.gameObject.scene);
            Debug.Log("전투 디버그 기즈모 제거 완료. Ctrl+S로 씬을 저장했다.");
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
            {
                Undo.AddComponent<T>(go);
            }
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }
    }
}
