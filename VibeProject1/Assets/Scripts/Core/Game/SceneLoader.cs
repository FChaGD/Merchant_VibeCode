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
            Transition(ToSceneName(initialScene));
        }

        public void Transition(string sceneName)
        {
            StartCoroutine(TransitionRoutine(sceneName));
        }

        private IEnumerator TransitionRoutine(string sceneName)
        {
            var previousSceneName = currentContentScene;

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (!string.IsNullOrEmpty(previousSceneName))
            {
                // 콘텐츠 씬마다 자체 EventSystem을 갖고 있다(각 씬 인스톨러가 생성). UnloadSceneAsync가
                // 끝나길 기다리는 동안 새 씬의 EventSystem과 이전 씬의 EventSystem이 한 프레임이라도
                // 동시에 존재하면 Unity가 "There can be only one active Event System" 경고와 함께
                // 입력을 먹통으로 만든다 - 그래서 언로드를 기다리지 않고 이전 EventSystem만 여기서
                // 즉시 파괴해 동시 존재 자체를 없앤다.
                RemoveEventSystem(SceneManager.GetSceneByName(previousSceneName));
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

        private static string ToSceneName(ContentSceneId id)
        {
            switch (id)
            {
                case ContentSceneId.Hub:
                    return SceneNames.Hub;
                case ContentSceneId.Field:
                    return SceneNames.Field;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }
}
