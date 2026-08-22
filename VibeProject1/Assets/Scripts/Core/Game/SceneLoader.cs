using System;
using System.Collections;
using UnityEngine;
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
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (!string.IsNullOrEmpty(currentContentScene))
            {
                yield return SceneManager.UnloadSceneAsync(currentContentScene);
            }

            currentContentScene = sceneName;
            OnSceneLoaded?.Invoke(sceneName);
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
