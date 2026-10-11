using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hub 날짜 HUD(Docs/설계/85번 §4.1) - 재화 HUD와 같은 구조로 UIManager에 붙고 HubUIWiring이 등록한다. 날짜는 Changed 이벤트로만 갱신한다.
    /// 날짜 매니저는 Bootstrap 상주라 Hub를 다시 방문할 때마다 이전 구독을 먼저 해제한다. Hub가 언로드된 뒤에도 Changed가 올 수 있어
    /// (상행 중 가산) 텍스트가 파괴됐으면 무시한다.
    /// </summary>
    public class GameDateHudController : MonoBehaviour, IGameDateHudController
    {
        private IDateReader dateReader;
        private TMP_Text dateText;

        public void RegisterDateUI(SceneUIRoot sceneUIRoot, IDateReader dateReader)
        {
            Unbind();

            if (!sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.DatePanelRoot, out var panelRoot)
                || !sceneUIRoot.TryGetElement<TMP_Text>(HubUIElementIds.DateText, out dateText))
            {
                Debug.LogWarning($"Hub UI에서 날짜 HUD 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라(Tools > Game > Build Hub Scene).");
                return;
            }

            // 날짜 매니저가 없으면(인스톨러 미실행) 패널을 숨긴다 - 틀린 날짜를 보여 주지 않는다.
            if (dateReader == null)
            {
                panelRoot.gameObject.SetActive(false);
                return;
            }

            panelRoot.gameObject.SetActive(true);
            this.dateReader = dateReader;
            dateReader.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (dateReader != null) dateReader.Changed -= Refresh;
            dateReader = null;
        }

        private void Refresh()
        {
            if (dateText == null || dateReader == null) return;
            dateText.text = dateReader.Today.ToDisplayString();
        }
    }
}
