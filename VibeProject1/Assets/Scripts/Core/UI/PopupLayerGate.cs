using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UI 관리의 팝업 축(Docs/설계/38번 §3·§6). 모달 팝업이 열리면 등록된 레이어의 CanvasGroup을 숨기고
    /// (alpha 0, 클릭/상호작용 차단), 닫히면 되돌린다.
    /// SetActive가 아니라 CanvasGroup을 쓰는 이유: depth 축이 같은 하위 오브젝트들을 SetActive로 켜고 끄므로,
    /// 두 축이 서로 다른 속성을 소유해야 한쪽이 다른 쪽 상태를 덮어쓰지 않는다.
    /// 순수 C# 객체가 아니라 UIManager 산하 컴포넌트인 이유: 영속 객체인 UIManager의 이벤트에 씬이 다시 로드될
    /// 때마다 새 구독이 쌓이지 않도록 같은 인스턴스가 이전 구독을 해제하고 다시 구독해야 한다.
    /// 어떤 레이어를 숨길지는 씬 배선(HubUIWiring 등)이 정한다 - 씬별 요소 Id를 이 클래스가 알지 않는다.
    /// </summary>
    public class PopupLayerGate : MonoBehaviour, IPopupLayerGate
    {
        private IUIVisibilitySignal signal;
        private readonly List<CanvasGroup> hiddenLayers = new();

        public void Register(IUIVisibilitySignal signal, IReadOnlyList<CanvasGroup> hiddenOnModalPopup)
        {
            if (this.signal != null)
            {
                this.signal.ModalPopupOpenChanged -= Apply;
            }

            this.signal = signal;
            hiddenLayers.Clear();
            hiddenLayers.AddRange(hiddenOnModalPopup);

            signal.ModalPopupOpenChanged += Apply;
            Apply(signal.IsModalPopupOpen);
        }

        private void Apply(bool isModalPopupOpen)
        {
            foreach (var layer in hiddenLayers)
            {
                // 씬 전환 직후 이전 씬의 레이어는 이미 파괴됐을 수 있다(채널 Reset 신호가 새 등록보다 먼저 온다).
                if (layer == null)
                {
                    continue;
                }

                layer.alpha = isModalPopupOpen ? 0f : 1f;
                layer.blocksRaycasts = !isModalPopupOpen;
                layer.interactable = !isModalPopupOpen;
            }
        }

        private void OnDestroy()
        {
            if (signal != null)
            {
                signal.ModalPopupOpenChanged -= Apply;
            }
        }
    }
}
