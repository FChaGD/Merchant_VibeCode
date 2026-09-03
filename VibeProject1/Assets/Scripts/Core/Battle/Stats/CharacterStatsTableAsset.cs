using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct CharacterStatsEntry
    {
        public MercenaryClass MercenaryClass;
        public float MaxHp;
        public float Attack;
        public float Defense;
        public float MoveSpeed;
        public float AttackInterval;
        public float Range;
        public float MoraleSyncRate;
    }

    /// <summary>
    /// 용병 직업별 스탯 - PlaceholderBattleUnitStatProvider의 하드코딩 Dictionary를 대체하는 실제
    /// 데이터(Docs/설계/17번 §3). 값은 인스펙터 수기 편집이 아니라 CharacterStatsTableImporter
    /// (Tools/Game/Table/Import Character Stats)가 Assets/Table/Character/CharacterStats.xlsx에서
    /// 채운다. 표시명은 CharacterStringsTableAsset으로 분리(Docs/설계/18번 §6).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStatsTable", menuName = "Game/Table/Character Stats Table")]
    public class CharacterStatsTableAsset : ScriptableObject
    {
        [SerializeField] private List<CharacterStatsEntry> entries = new();

        public bool TryGetEntry(MercenaryClass mercenaryClass, out CharacterStatsEntry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate.MercenaryClass == mercenaryClass)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IReadOnlyList<CharacterStatsEntry> Entries => entries;
    }
}
