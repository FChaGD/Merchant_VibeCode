using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 적 구성 사전 편집(요구사항 #6) - 타입 3종 각각 개수를 입력하고 적용하면 BattleTestEnemyRoster를
    /// 통째로 다시 채운다(전투 중 드래그 추가는 ILiveUnitSpawner를 통해 개별 추가하는 별개 기능,
    /// 계획의 "적 구성 편집과 전투 중 유닛 추가는 별개 UI" 확인 사항). 위치는 실제 게임과 같은 방식
    /// (UniformRandomSpawnPointSelector + 스폰 반지름)으로 무작위 배정한다.
    /// </summary>
    public class BattleTestEnemySetupView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField marauderCountInput;
        [SerializeField] private TMP_InputField monsterCountInput;
        [SerializeField] private TMP_InputField adversaryCountInput;
        [SerializeField] private Button applyButton;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private void Start()
        {
            applyButton.onClick.AddListener(HandleApplyClicked);
        }

        // 로스터를 직접 건드리지 않고 BattleTestSimulationRule.ReplaceEnemyComposition을 거친다 -
        // 그래야 미리보기 뷰 정리/재생성 이벤트(OnEnemyRosterCleared/OnUnitAdded)가 함께 발행된다.
        private void HandleApplyClicked()
        {
            simulationRule.ReplaceEnemyComposition(
                ParseCountOrZero(marauderCountInput),
                ParseCountOrZero(monsterCountInput),
                ParseCountOrZero(adversaryCountInput));
        }

        private static int ParseCountOrZero(TMP_InputField field)
            => int.TryParse(field.text, out var count) && count > 0 ? count : 0;
    }
}
