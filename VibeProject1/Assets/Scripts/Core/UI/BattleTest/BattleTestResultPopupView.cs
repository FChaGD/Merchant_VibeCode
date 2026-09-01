using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 화면 중앙 하단에 작게 뜨는 결과 배지 - 기존 FieldResultPopupView(전체화면 모달+확인 버튼)와
    /// 달리 세팅 조작(팔레트 드래그 등)을 막지 않아야 해서 별도로 둔다. 승패 확정 시 표시하고,
    /// 전투 리셋 시 지운다(BattleTestController가 IResettableBattleSimulation.OnReset 구독).
    /// </summary>
    public class BattleTestResultPopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageLabel;

        public void Show(BattleResult result)
        {
            messageLabel.text = result.Outcome == BattleOutcome.Victory ? "승리" : "패배";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
