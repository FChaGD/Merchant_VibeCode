using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 이벤트·대화 이력 디버그(EncounterDebugView)를 FieldManager에 설치/제거한다(설계 87 §8.2). 다른 디버그 도구와 같은 이유로 ManagerHierarchyInstaller와
    /// 분리했다 - "게임 빌드"와 "디버그 도구 켜고 끄기"는 다른 관심사다. Bootstrap.unity를 열고 실행한다.
    /// </summary>
    public static class EncounterDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Encounter History Debug")]
        public static void Install()
        {
            var fieldManager = Object.FindFirstObjectByType<FieldManager>(FindObjectsInactive.Include);
            if (fieldManager == null)
            {
                Debug.LogWarning("씬에서 FieldManager를 찾을 수 없다 - Bootstrap.unity를 열고 Build Bootstrap Scene 후 다시 실행하라.");
                return;
            }
            if (fieldManager.GetComponent<EncounterDebugView>() == null) Undo.AddComponent<EncounterDebugView>(fieldManager.gameObject);
            EditorSceneManager.MarkSceneDirty(fieldManager.gameObject.scene);
            Debug.Log("이벤트·대화 이력 디버그 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Encounter History Debug")]
        public static void Remove()
        {
            var fieldManager = Object.FindFirstObjectByType<FieldManager>(FindObjectsInactive.Include);
            if (fieldManager == null) return;
            var component = fieldManager.GetComponent<EncounterDebugView>();
            if (component != null) Undo.DestroyObjectImmediate(component);
            EditorSceneManager.MarkSceneDirty(fieldManager.gameObject.scene);
            Debug.Log("이벤트·대화 이력 디버그 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
