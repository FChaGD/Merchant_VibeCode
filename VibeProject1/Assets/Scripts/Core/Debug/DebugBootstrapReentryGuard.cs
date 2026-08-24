#if UNITY_EDITOR
using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// Hub/Field 등 콘텐츠 씬을 Bootstrap 없이 단독 Play했을 때(매니저 부재) 자동으로
    /// Bootstrap을 거쳐 원래 씬으로 되돌려주는 에디터 전용 디버그 도구. 정상 흐름(Bootstrap
    /// 경유)에서는 BootstrapRoot가 이미 존재하므로 Awake에서 즉시 자기 파괴되어 아무 영향도 없다.
    /// 네임스페이스를 "Debug"가 아닌 "DebugTools"로 둔 것은 UnityEngine.Debug와의 이름 충돌을 피하기 위함.
    /// </summary>
    public class DebugBootstrapReentryGuard : MonoBehaviour
    {
        private const string BootstrapSceneName = "Bootstrap";

        private void Awake()
        {
            if (FindFirstObjectByType<BootstrapRoot>() != null)
            {
                Destroy(gameObject);
                return;
            }

            var targetSceneName = gameObject.scene.name;

            // Bootstrap 씬을 Single로 로드하면 현재 씬은 파괴되지만, DontDestroyOnLoad 오브젝트는
            // 별도의 유사 씬에 있어 살아남는다 - 이 위에서 재진입 시퀀스를 이어간다.
            DontDestroyOnLoad(gameObject);
            StartCoroutine(ReenterThroughBootstrap(targetSceneName));
        }

        private IEnumerator ReenterThroughBootstrap(string targetSceneName)
        {
            yield return SceneManager.LoadSceneAsync(BootstrapSceneName, LoadSceneMode.Single);

            var dependencyManager = FindFirstObjectByType<DependencyManager>();
            if (dependencyManager == null || !dependencyManager.TryResolve<ISceneLoader>(out var sceneLoader))
            {
                Debug.LogWarning($"{nameof(DebugBootstrapReentryGuard)}: ISceneLoader를 찾지 못해 '{targetSceneName}'으로 복귀할 수 없다.");
                Destroy(gameObject);
                yield break;
            }

            // SceneLoader가 Bootstrap 초기화 직후 기본 initialScene(Hub 등)으로 1차 전환을 이미
            // 시작한 상태 - 그 결과를 기다렸다가 목표 씬과 다르면 재전환을 요청한다(SceneLoader는 수정하지 않는다).
            void HandleSceneLoaded(string loadedSceneName)
            {
                if (loadedSceneName != targetSceneName)
                {
                    sceneLoader.Transition(targetSceneName);
                    return;
                }

                sceneLoader.OnSceneLoaded -= HandleSceneLoaded;
                Destroy(gameObject);
            }

            sceneLoader.OnSceneLoaded += HandleSceneLoaded;
        }
    }
}
#endif
