using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Bootstrap 씬의 Managers 루트에 부착. 콘텐츠 씬이 Additive로 교체되는 동안
    /// 매니저 계층 전체가 파괴되지 않도록 유지하는 것이 유일한 책임이다.
    /// </summary>
    public class BootstrapRoot : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
