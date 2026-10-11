using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 날짜 진행 디버그 버튼(GameDateDebugView)을 DateManager와 같은 GameObject에 설치/제거한다. "게임 빌드"와 "디버그 도구 켜고 끄기"는
    /// 다른 관심사라 ManagerHierarchyInstaller와 분리했다. Build Bootstrap Scene이 DateManager를 get-or-create로 재사용하므로 게임 빌드를
    /// 다시 돌려도 설치 상태가 유지된다. Bootstrap.unity를 열고 실행해야 한다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 한 번 실행한 뒤(안 하면 Missing Script가 남는다) 이 파일과 GameDateDebugView.cs(+.meta)를 지우고
    /// DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class GameDateDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Date Advance Buttons")]
        public static void InstallDateButtons()
        {
            var dateManager = Object.FindFirstObjectByType<DateManager>(FindObjectsInactive.Include);
            if (dateManager == null)
            {
                Debug.LogWarning("씬에서 DateManager를 찾을 수 없다 - Bootstrap.unity를 열고 Build Bootstrap Scene을 먼저 실행하라.");
                return;
            }

            if (dateManager.gameObject.GetComponent<GameDateDebugView>() == null)
            {
                Undo.AddComponent<GameDateDebugView>(dateManager.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(dateManager.gameObject.scene);
            Debug.Log("날짜 진행 디버그 버튼 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Date Advance Buttons")]
        public static void RemoveDateButtons()
        {
            var dateManager = Object.FindFirstObjectByType<DateManager>(FindObjectsInactive.Include);
            if (dateManager == null)
            {
                return;
            }

            var component = dateManager.gameObject.GetComponent<GameDateDebugView>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            EditorSceneManager.MarkSceneDirty(dateManager.gameObject.scene);
            Debug.Log("날짜 진행 디버그 버튼 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
