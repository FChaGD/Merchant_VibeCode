using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 세팅 단계 유닛을 클릭하면 뜨는 패널 - 수치값을 조회/조절하고, "배치 취소"로 해당 로스터
    /// 항목과 뷰를 함께 지운다. 뷰 파괴는 이 패널이 직접 처리한다(클릭 시점에 이미
    /// BattleTestUnitClickTarget 참조를 들고 있으므로 별도 이벤트 왕복 없이 단순하게 끝난다).
    /// "적용"은 로스터 항목의 StatsOverride만 갱신한다 - BattleCharacterUnit.stats는 생성 후
    /// 불변이라 미리보기 스프라이트 크기가 즉시 다시 그려지진 않는다(다음 리셋/전투 시작부터 반영,
    /// 계획된 단순화).
    /// </summary>
    public class BattleTestUnitInfoPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_InputField maxHpInput;
        [SerializeField] private TMP_InputField attackInput;
        [SerializeField] private TMP_InputField defenseInput;
        [SerializeField] private TMP_InputField moveSpeedInput;
        [SerializeField] private TMP_InputField attackIntervalInput;
        [SerializeField] private TMP_InputField rangeInput;
        [SerializeField] private TMP_InputField moraleSyncRateInput;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelPlacementButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private BattleTestUnitClickTarget currentTarget;
        // "적용" 시 입력칸에 없는 값(HpRegenPerSecond 등)을 원래 값 그대로 유지하기 위해 보관한다 -
        // 안 그러면 조절 UI가 없는 필드가 조용히 0으로 리셋된다.
        private BattleUnitStats originalStats;

        private void Awake()
        {
            applyButton.onClick.AddListener(HandleApplyClicked);
            cancelPlacementButton.onClick.AddListener(HandleCancelPlacementClicked);
            closeButton.onClick.AddListener(Hide);
        }

        public void Show(BattleTestUnitClickTarget target)
        {
            currentTarget = target;
            originalStats = ResolveCurrentStats(target);

            titleLabel.text = target.IsAlly ? "아군 유닛" : "적 유닛";
            maxHpInput.text = originalStats.MaxHp.ToString("0.##");
            attackInput.text = originalStats.Attack.ToString("0.##");
            defenseInput.text = originalStats.Defense.ToString("0.##");
            moveSpeedInput.text = originalStats.MoveSpeed.ToString("0.##");
            attackIntervalInput.text = originalStats.AttackInterval.ToString("0.##");
            rangeInput.text = originalStats.Range.ToString("0.##");
            moraleSyncRateInput.text = originalStats.MoraleSyncRate.ToString("0.##");

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            currentTarget = null;
            gameObject.SetActive(false);
        }

        private BattleUnitStats ResolveCurrentStats(BattleTestUnitClickTarget target)
        {
            if (target.IsAlly)
            {
                if (simulationRule.AllyRoster.TryGet(target.EntryId, out var entry))
                {
                    return entry.StatsOverride ?? simulationRule.GetAllyDefaultStats(entry.Class);
                }
            }
            else if (simulationRule.EnemyRoster.TryGet(target.EntryId, out var entry))
            {
                return entry.StatsOverride ?? simulationRule.GetEnemyDefaultStats(entry.Type);
            }

            return default;
        }

        private void HandleApplyClicked()
        {
            if (currentTarget == null) return;

            var stats = new BattleUnitStats(
                maxHp: ParseOr(maxHpInput, originalStats.MaxHp),
                attack: ParseOr(attackInput, originalStats.Attack),
                defense: ParseOr(defenseInput, originalStats.Defense),
                moveSpeed: ParseOr(moveSpeedInput, originalStats.MoveSpeed),
                attackInterval: ParseOr(attackIntervalInput, originalStats.AttackInterval),
                range: ParseOr(rangeInput, originalStats.Range),
                moraleSyncRate: ParseOr(moraleSyncRateInput, originalStats.MoraleSyncRate),
                hpRegenPerSecond: originalStats.HpRegenPerSecond,
                enemyType: originalStats.EnemyType);

            if (currentTarget.IsAlly) simulationRule.SetAllyStatsOverride(currentTarget.EntryId, stats);
            else simulationRule.SetEnemyStatsOverride(currentTarget.EntryId, stats);

            Hide();
        }

        private void HandleCancelPlacementClicked()
        {
            if (currentTarget == null) return;

            if (currentTarget.IsAlly) simulationRule.RemoveAlly(currentTarget.EntryId);
            else simulationRule.RemoveEnemy(currentTarget.EntryId);

            Destroy(currentTarget.gameObject);
            Hide();
        }

        private static float ParseOr(TMP_InputField field, float fallback)
            => float.TryParse(field.text, out var value) ? value : fallback;
    }
}
