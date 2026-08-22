using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 씬의 Canvas에 부착하는 범용 UI 루트. 하위의 UIElementMarker를 자동 수집해
    /// 어떤 씬, 어떤 종류의 UI 요소든 동일한 방식으로 조회할 수 있게 한다.
    /// UI/씬이 늘어나도 이 클래스를 그대로 재사용한다 — 새로 필요한 건 각 씬의
    /// 컨트롤러(예: HubUIController)가 수행하는 실제 연결 로직뿐이다.
    /// </summary>
    public class SceneUIRoot : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> elementsById = new();

        private void Awake()
        {
            foreach (var marker in GetComponentsInChildren<UIElementMarker>(true))
            {
                if (string.IsNullOrEmpty(marker.Id))
                {
                    Debug.LogWarning($"Id가 비어있는 {nameof(UIElementMarker)}가 있다: {marker.name}", marker);
                    continue;
                }

                if (!elementsById.TryAdd(marker.Id, marker.gameObject))
                {
                    Debug.LogWarning($"'{marker.Id}'에 해당하는 UI 요소가 중복 등록되어 있다: {marker.name}", marker);
                }
            }
        }

        public bool TryGetElement<T>(string id, out T component) where T : Component
        {
            if (elementsById.TryGetValue(id, out var go))
            {
                component = go.GetComponent<T>();
                return component != null;
            }

            component = null;
            return false;
        }
    }
}
