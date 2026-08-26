using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// GameManager 산하 컴포넌트. 콘텐츠 씬을 Additive로 로드하고 이전 콘텐츠 씬을 언로드한다.
    /// 매니저가 상주하는 Bootstrap 씬은 절대 언로드하지 않는다.
    /// </summary>
    public class SceneLoader : MonoBehaviour, ISceneLoader, IManagedComponent
    {
        [Tooltip("Bootstrap 초기설정 완료 직후 최초로 전환할 콘텐츠 씬.")]
        [SerializeField] private ContentSceneId initialScene = ContentSceneId.Hub;

        private string currentContentScene;

        public event Action<string> OnSceneLoaded;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ISceneLoader>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 매니징 컴포넌트 초기설정이 끝난 직후 지정된 콘텐츠 씬으로 최초 전환한다.
            Transition(initialScene.ToString());
        }

        public void Transition(string sceneName)
        {
            StartCoroutine(TransitionRoutine(sceneName));
        }

        private IEnumerator TransitionRoutine(string sceneName)
        {
            var previousSceneName = currentContentScene;

            // 콘텐츠 씬마다 자체 EventSystem과 Camera(AudioListener 포함)를 갖고 있다(각 씬에 이미
            // 구성돼 있음). 예전에는 새 씬을 먼저 로드한 뒤 이전 것들을 파괴했는데, LoadSceneAsync가
            // 끝나는 순간 새 씬의 EventSystem/AudioListener가 곧바로 활성화돼 그 시점엔 이전 것들이
            // 아직 살아있어 "There can be only one active Event System"/"2 audio listeners" 경고가
            // 매번 떴다 - 새 씬을 로드하기 전에 이전 것들부터 파괴해 동시에 존재하는 프레임 자체를 없앤다.
            if (!string.IsNullOrEmpty(previousSceneName))
            {
                var previousScene = SceneManager.GetSceneByName(previousSceneName);
                RemoveEventSystem(previousScene);
                RemoveAudioListener(previousScene);

                // Destroy()는 즉시 사라지지 않고 이번 프레임 끝에 처리된다 - 한 프레임을 명시적으로
                // 기다려, 새 씬이 로드되며 자신의 EventSystem/AudioListener를 활성화하는 시점에는
                // 이전 것들이 확실히 없는 상태이도록 보장한다.
                yield return null;
            }

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (!string.IsNullOrEmpty(previousSceneName))
            {
                yield return SceneManager.UnloadSceneAsync(previousSceneName);
            }

            currentContentScene = sceneName;
            OnSceneLoaded?.Invoke(sceneName);
        }

        private static void RemoveEventSystem(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<EventSystem>() != null)
                {
                    Destroy(rootObject);
                }
            }
        }

        // Camera 자체(렌더링)는 남기고 AudioListener 컴포넌트만 제거한다 - EventSystem과 달리
        // Camera까지 통째로 사라지면 언로드 전까지 그 씬의 Canvas가 잠깐 그려지지 않을 수 있다.
        private static void RemoveAudioListener(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var listener = rootObject.GetComponentInChildren<AudioListener>(true);
                if (listener != null)
                {
                    Destroy(listener);
                }
            }
        }
    }
}
