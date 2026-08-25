using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 진영 하나의 전체 사기(기획 §7 PartyMorale). Character 유닛이 전투 불능(사망/도주)이 될 때마다
    /// 하락한다. 전투마다 새로 생성된다 - 이전 전투의 사기가 이어지지 않는다.
    /// </summary>
    public class PartyMorale
    {
        public float CurrentValue { get; private set; } = MoraleTuning.Initial;

        public void NotifyUnitLost() =>
            CurrentValue = Mathf.Max(0f, CurrentValue - MoraleTuning.PartyMoraleLossOnUnitLost);
    }
}
