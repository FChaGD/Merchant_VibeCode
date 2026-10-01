using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 상행 디버그 버튼(TripDebugView - 시간 배속·인카운터 발생·상행 완료)을 GameManager와 같은 GameObject에 설치/제거한다(Docs/설계/76번 §8). 다른 디버그 도구와
    /// 같은 이유로 ManagerHierarchyInstaller와 분리했다 - "게임 빌드"와 "디버그 도구 켜고 끄기"는 다른 관심사다. Bootstrap.unity를 열고 실행한다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤 이 파일과 TripDebugView.cs(+.meta)·EncounterManager.DebugTriggerEncounter를 지우고 DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class TripDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Trip Debug Buttons")]
        public static void InstallTripDebugButtons()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager == null)
            {
                Debug.LogWarning("씬에서 GameManager를 찾을 수 없다 - Bootstrap.unity를 열고 다시 실행하라.");
                return;
            }

            if (gameManager.GetComponent<TripDebugView>() == null)
            {
                Undo.AddComponent<TripDebugView>(gameManager.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
            Debug.Log("상행 디버그 버튼 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Trip Debug Buttons")]
        public static void RemoveTripDebugButtons()
        {
            var gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager == null)
            {
                return;
            }

            var component = gameManager.GetComponent<TripDebugView>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            EditorSceneManager.MarkSceneDirty(gameManager.gameObject.scene);
            Debug.Log("상행 디버그 버튼 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
