namespace Game.Core
{
    /// <summary>
    /// Docs/기획/08-2026-09-01-전투_해석로직_기획.md §5.3 - 방어력이 공격력의 0.7배 이하면 그대로 뺀 값,
    /// 넘어서면 0.21×공격력²/방어력으로 반비례 감쇠한다(분기점에서 두 식의 값이 정확히 일치).
    /// 기획 단계에서 이 공식이 세 번 바뀐 만큼 BattleCharacterUnit에서 분리해 별도 전략으로 둔다.
    /// </summary>
    public class PlaceholderDamageFormula : IDamageFormula
    {
        public float ComputeDamage(float attack, float defense)
        {
            var breakpoint = attack * 0.7f;
            return defense <= breakpoint
                ? attack - defense
                : 0.21f * attack * attack / defense;
        }
    }
}
