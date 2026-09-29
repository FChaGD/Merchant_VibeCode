using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 마을 정비창 대열 외곽선(FormationAreaOutlineDebugView)을 UIManager 오브젝트에 설치/제거한다. HubFormationPanel이 같은 오브젝트에 있어
    /// GetComponent로 찾는다. 게임 빌드 인스톨러와 분리한 이유는 다른 디버그 도구와 같다. Bootstrap.unity를 열고 실행해야 한다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤(안 하면 씬에 Missing Script가 남는다) 이 파일과 FormationAreaOutlineDebugView,
    /// DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class FormationAreaOutlineDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Formation Area Outline")]
        public static void InstallOutline()
        {
            var uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager == null)
            {
                Debug.LogWarning("씬에서 UIManager를 찾을 수 없다 - Bootstrap.unity를 열고 다시 실행하라.");
                return;
            }

            if (uiManager.gameObject.GetComponent<FormationAreaOutlineDebugView>() == null)
            {
                Undo.AddComponent<FormationAreaOutlineDebugView>(uiManager.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(uiManager.gameObject.scene);
            Debug.Log("정비창 대열 외곽선 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Formation Area Outline")]
        public static void RemoveOutline()
        {
            var uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager == null)
            {
                return;
            }

            var component = uiManager.gameObject.GetComponent<FormationAreaOutlineDebugView>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            EditorSceneManager.MarkSceneDirty(uiManager.gameObject.scene);
            Debug.Log("정비창 대열 외곽선 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
