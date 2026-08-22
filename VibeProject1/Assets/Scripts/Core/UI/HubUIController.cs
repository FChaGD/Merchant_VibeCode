using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Hub 씬이 로드된 시점에 Hub 씬의 SceneUIRoot를 찾아
    /// 버튼 클릭 동작과 배경 이미지를 연결한다.
    /// </summary>
    public class HubUIController : MonoBehaviour, IHubUIController
    {
        [SerializeField] private Sprite backgroundSprite;

        public void RegisterHubUI(IGameManager gameManager, IUIManager uiManager)
        {
            var hubScene = SceneManager.GetSceneByName(SceneNames.Hub);
            if (!hubScene.IsValid())
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬을 찾을 수 없어 Hub UI를 등록하지 못했다.");
                return;
            }

            SceneUIRoot sceneUIRoot = null;
            foreach (var rootObject in hubScene.GetRootGameObjects())
            {
                sceneUIRoot = rootObject.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null)
                {
                    break;
                }
            }

            if (sceneUIRoot == null)
            {
                Debug.LogWarning($"'{SceneNames.Hub}' 씬에서 {nameof(SceneUIRoot)}를 찾을 수 없다.");
                return;
            }

            BindButton(sceneUIRoot, HubUIElementIds.DepartureButton, () => gameManager.RequestSceneTransition(SceneNames.Field));
            BindButton(sceneUIRoot, HubUIElementIds.FormationButton, () => uiManager.Open(UIPanelIds.Formation));

            ApplyBackground(sceneUIRoot);
        }

        private void ApplyBackground(SceneUIRoot sceneUIRoot)
        {
            if (backgroundSprite == null)
            {
                return;
            }

            if (!sceneUIRoot.TryGetElement<Image>(HubUIElementIds.Background, out var background))
            {
                Debug.LogWarning($"Hub UI에서 배경 Image('{HubUIElementIds.Background}')를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
                return;
            }

            background.sprite = backgroundSprite;
        }

        private static void BindButton(SceneUIRoot sceneUIRoot, string id, UnityAction action)
        {
            if (!sceneUIRoot.TryGetElement<Button>(id, out var button))
            {
                Debug.LogWarning($"Hub UI에서 '{id}' 버튼을 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
