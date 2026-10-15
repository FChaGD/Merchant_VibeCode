using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 내구도 디버그(CaravanAssetDurabilityDebugView)를 CaravanAssetDurabilityManager에 설치/제거한다(설계 91번 §7). "게임 빌드"와 "디버그 도구 켜고 끄기"는
    /// 다른 관심사라 ManagerHierarchyInstaller와 분리했다. Build Bootstrap Scene이 매니저를 get-or-create로 재사용하므로 다시 빌드해도 설치 상태가 유지된다.
    /// Bootstrap.unity를 열고 실행한다.
    /// </summary>
    public static class CaravanAssetDurabilityDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Caravan Durability Debug")]
        public static void Install()
        {
            var manager = Object.FindFirstObjectByType<CaravanAssetDurabilityManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogWarning("씬에서 CaravanAssetDurabilityManager를 찾을 수 없다 - Bootstrap.unity를 열고 Build Bootstrap Scene 후 다시 실행하라.");
                return;
            }
            if (manager.GetComponent<CaravanAssetDurabilityDebugView>() == null) Undo.AddComponent<CaravanAssetDurabilityDebugView>(manager.gameObject);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log("내구도 디버그 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Caravan Durability Debug")]
        public static void Remove()
        {
            var manager = Object.FindFirstObjectByType<CaravanAssetDurabilityManager>(FindObjectsInactive.Include);
            if (manager == null) return;
            var component = manager.GetComponent<CaravanAssetDurabilityDebugView>();
            if (component != null) Undo.DestroyObjectImmediate(component);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log("내구도 디버그 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
