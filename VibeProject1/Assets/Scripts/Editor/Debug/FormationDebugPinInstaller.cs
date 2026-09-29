using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 정비창 디버그 핀 저장소(FormationDebugPinStore)를 UIManager 오브젝트에 설치/제거한다(Docs/설계/60번 §8). 마을·상행 배치 패널
    /// (HubFormationPanel/FieldFormationPanel)이 같은 오브젝트에 있어 GetComponent로 찾는다. 게임 빌드 인스톨러(ManagerHierarchyInstaller)와
    /// 분리한 이유는 다른 디버그 도구와 같다 - "게임 빌드"와 "디버그 도구 켜고 끄기"는 다른 관심사다. UIManager 오브젝트는 Build Bootstrap
    /// Scene이 재사용하므로 게임 빌드를 다시 돌려도 설치 상태가 유지된다. Bootstrap.unity를 열고 실행해야 한다.
    /// 핀을 놓는 패널(격자 위 디버그 패널)은 정비창 화면 인스톨러가 만든다 - 저장소가 없으면 패널은 보이지만 핀이 저장되지 않는다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤(안 하면 씬에 Missing Script가 남는다) 이 파일과 Core/Debug/Formation의 핀 관련 파일,
    /// DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class FormationDebugPinInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Formation Debug Pins")]
        public static void InstallPins()
        {
            var uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager == null)
            {
                Debug.LogWarning("씬에서 UIManager를 찾을 수 없다 - Bootstrap.unity를 열고 다시 실행하라.");
                return;
            }

            if (uiManager.gameObject.GetComponent<FormationDebugPinStore>() == null)
            {
                Undo.AddComponent<FormationDebugPinStore>(uiManager.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(uiManager.gameObject.scene);
            Debug.Log("정비창 디버그 핀 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Formation Debug Pins")]
        public static void RemovePins()
        {
            var uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager == null)
            {
                return;
            }

            var component = uiManager.gameObject.GetComponent<FormationDebugPinStore>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            EditorSceneManager.MarkSceneDirty(uiManager.gameObject.scene);
            Debug.Log("정비창 디버그 핀 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
