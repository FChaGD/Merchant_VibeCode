using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// GameManager 산하 컴포넌트. 콘텐츠 씬을 Additive로 로드하고 이전 콘텐츠 씬을 언로드한다.
    /// 매니저가 상주하는 Bootstrap 씬은 절대 언로드하지 않는다.
    /// </summary>
    public class SceneLoader : MonoBehaviour, ISceneLoader
    {
        private string currentContentScene;

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
        }
    }
}
