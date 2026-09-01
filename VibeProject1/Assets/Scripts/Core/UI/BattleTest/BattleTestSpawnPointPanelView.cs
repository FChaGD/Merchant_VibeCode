using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 스폰 포인트 마커를 클릭하면 뜨는 예약 설정 패널 - 적 구성 편집 패널(BattleTestEnemySetupView)과
    /// 같은 "타입별 개수 입력 + 적용" 방식이다(사용자 요청: 한 지점에서 여러 마리를 예약할 수
    /// 있어야 함). 적용을 눌러도 그 즉시 필드에 유닛이 나타나지 않는다 - 여기서는
    /// BattleTestSpawnPointReservations에 예약만 기록되고, 실제 유닛 생성은
    /// BattleTestSimulationRule.Evaluate()가 전투를 시작할 때 예약을 로스터로 변환하면서 이뤄진다.
    /// </summary>
    public class BattleTestSpawnPointPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_InputField marauderCountInput;
        [SerializeField] private TMP_InputField monsterCountInput;
        [SerializeField] private TMP_InputField adversaryCountInput;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private int currentSpawnPointIndex = -1;

        private void Awake()
        {
            applyButton.onClick.AddListener(HandleApplyClicked);
            closeButton.onClick.AddListener(Hide);
        }

        public void Show(BattleTestSpawnPointMarkerView marker)
        {
            currentSpawnPointIndex = marker.SpawnPointIndex;
            titleLabel.text = $"스폰 포인트 #{currentSpawnPointIndex}";

            var composition = simulationRule.SpawnPointReservations.Get(currentSpawnPointIndex);
            marauderCountInput.SetTextWithoutNotify(composition.Marauder.ToString());
            monsterCountInput.SetTextWithoutNotify(composition.Monster.ToString());
            adversaryCountInput.SetTextWithoutNotify(composition.Adversary.ToString());

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            currentSpawnPointIndex = -1;
            gameObject.SetActive(false);
        }

        private void HandleApplyClicked()
        {
            if (currentSpawnPointIndex < 0) return;

            var composition = new BattleTestSpawnPointReservations.Composition(
                ParseCountOrZero(marauderCountInput),
                ParseCountOrZero(monsterCountInput),
                ParseCountOrZero(adversaryCountInput));

            simulationRule.SpawnPointReservations.Set(currentSpawnPointIndex, composition);
            Hide();
        }

        private static int ParseCountOrZero(TMP_InputField field) => int.TryParse(field.text, out var value) ? Mathf.Max(0, value) : 0;
    }
}
