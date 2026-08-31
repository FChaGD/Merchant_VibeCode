using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 진영 하나의 전체 사기(기획 08번 §7). 트리거 메서드가 배율까지 반영한 "실제 적용된 델타"를
    /// 반환한다 - 호출부(BattleCharacterUnit)가 이 값 그대로 사기 파동(§7.3)을 생성하는 데 쓴다.
    /// 전투마다 새로 생성된다 - 이전 전투의 사기가 이어지지 않는다.
    /// </summary>
    public class PartyMorale
    {
        public float CurrentValue { get; private set; } = MoraleTuning.Initial;
        public MoraleTier CurrentTier => CurrentValue.ToMoraleTier();

        public float NotifyDeath() => ApplyDelta(-MoraleTuning.DeathLoss);
        public float NotifyFled() => ApplyDelta(-MoraleTuning.FleeLoss);
        public float NotifyEnemyKilled() => ApplyDelta(MoraleTuning.EnemyKilledGain);

        // 이벤트 적용 "직전" 티어를 기준으로 배율을 매긴다(기획 §7.2) - CurrentTier를 먼저 읽고 나서
        // CurrentValue를 갱신하는 순서가 중요하다. 반환값은 클램프 이전의 논리적 델타다 - CurrentValue가
        // 이미 3인데 -15가 들어오면 실제 값은 0에서 멈추지만 반환값은 -15 그대로다. 파동이 나르는 값과
        // 유닛 개인 목표치의 클램프는 파동 수신 시점(BattleCharacterUnit.ReceiveMoraleWave)에서 따로
        // 처리하므로, 여기서 델타를 미리 잘라낼 이유가 없다(SRP).
        private float ApplyDelta(float rawDelta)
        {
            var amplified = CurrentTier == MoraleTier.Low ? rawDelta * MoraleTuning.LowTierAmplifier : rawDelta;
            CurrentValue = Mathf.Clamp(CurrentValue + amplified, 0f, 100f);
            return amplified;
        }
    }
}
