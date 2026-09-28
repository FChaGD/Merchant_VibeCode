using Game.Core;
using Game.Core.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// 재화 증감 디버그 버튼(PlayerCurrencyDebugSpinView)을 재화 지갑과 같은 GameObject에 설치/제거한다.
    /// BattleDebugResultInstaller와 같은 이유로 ManagerHierarchyInstaller와 분리했다 - "게임 빌드"와
    /// "디버그 도구 켜고 끄기"는 다른 관심사다. 지갑 오브젝트는 Build Bootstrap Scene이 get-or-create로
    /// 재사용하므로 게임 빌드를 다시 돌려도 설치 상태가 유지된다. Bootstrap.unity를 열고 실행해야 한다
    /// (지갑이 그 씬 하나에만 있음).
    /// 걷어낼 때는 Remove 메뉴를 먼저 한 번 실행한 뒤(안 하면 씬에 Missing Script가 남는다) 이 파일과
    /// PlayerCurrencyDebugSpinView.cs(+.meta)를 지우고 DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public static class PlayerCurrencyDebugInstaller
    {
        [MenuItem("Tools/Game/Debug/Install/Currency Spin Buttons")]
        public static void InstallSpinButtons()
        {
            var wallet = Object.FindFirstObjectByType<InMemoryPlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (wallet == null)
            {
                Debug.LogWarning("씬에서 InMemoryPlayerCurrencyWallet을 찾을 수 없다 - Bootstrap.unity를 열고 다시 실행하라.");
                return;
            }

            if (wallet.gameObject.GetComponent<PlayerCurrencyDebugSpinView>() == null)
            {
                Undo.AddComponent<PlayerCurrencyDebugSpinView>(wallet.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(wallet.gameObject.scene);
            Debug.Log("재화 증감 디버그 버튼 설치 완료. Ctrl+S로 씬을 저장하라.");
        }

        [MenuItem("Tools/Game/Debug/Remove/Currency Spin Buttons")]
        public static void RemoveSpinButtons()
        {
            var wallet = Object.FindFirstObjectByType<InMemoryPlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (wallet == null)
            {
                return;
            }

            var component = wallet.gameObject.GetComponent<PlayerCurrencyDebugSpinView>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }

            EditorSceneManager.MarkSceneDirty(wallet.gameObject.scene);
            Debug.Log("재화 증감 디버그 버튼 제거 완료. Ctrl+S로 씬을 저장하라.");
        }
    }
}
