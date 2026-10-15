using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 상행 대화 디버그(DialogueDebugView)를 FieldManager에 설치/제거한다(설계 88 §8). "게임 빌드"와 "디버그 도구 켜고 끄기"는 다른 관심사라 ManagerHierarchyInstaller와
    /// 분리했다. Build Bootstrap Scene이 FieldManager를 get-or-create로 재사용하므로 다시 빌드해도 설치 상태가 유지된다. Bootstrap.unity를 열고 실행한다.
    /// </summary>
    public static class DialogueDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Dialogue Debug")]
        public static void Install()
        {
            var fieldManager = Object.FindFirstObjectByType<FieldManager>(FindObjectsInactive.Include);
            if (fieldManager == null)
            {
                Debug.LogWarning("씬에서 FieldManager를 찾을 수 없다 - Bootstrap.unity를 열고 Build Bootstrap Scene 후 다시 실행하라.");
                return;
            }
            if (fieldManager.GetComponent<DialogueDebugView>() == null) Undo.AddComponent<DialogueDebugView>(fieldManager.gameObject);
            EditorSceneManager.MarkSceneDirty(fieldManager.gameObject.scene);
            Debug.Log("상행 대화 디버그 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Dialogue Debug")]
        public static void Remove()
        {
            var fieldManager = Object.FindFirstObjectByType<FieldManager>(FindObjectsInactive.Include);
            if (fieldManager == null) return;
            var component = fieldManager.GetComponent<DialogueDebugView>();
            if (component != null) Undo.DestroyObjectImmediate(component);
            EditorSceneManager.MarkSceneDirty(fieldManager.gameObject.scene);
            Debug.Log("상행 대화 디버그 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
