using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 씬 내 UI 요소(버튼, 이미지 등 종류를 가리지 않음)에 문자열 식별자를 부여하는 범용 마커.
    /// SceneUIRoot가 이 컴포넌트를 자동 수집하므로, UI가 추가되거나 씬이 새로 생기더라도
    /// 전용 마커/루트 클래스를 새로 작성할 필요 없이 이 컴포넌트를 붙이고 Id만 지정하면 된다.
    /// </summary>
    public class UIElementMarker : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;
    }
}
