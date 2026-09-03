using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    // 열린 제네릭(OptionEntry<TEnum>)은 유니티 시리얼라이저가 인스펙터에 안정적으로 그려주지 않아
    // (설계 문서 §2.1의 제네릭 스케치를 그대로 쓰지 않고) 축마다 닫힌 타입으로 나눴다 - 기획자가
    // 인스펙터에서 바로 편집 가능해야 한다는 요구(Docs/설계/11번 §8-1)를 지키기 위한 구현 선택.
    // DisplayLabel은 RoleGroupTacticsStringsTableAsset으로 분리됐다(Docs/설계/18번 §6).
    [Serializable]
    public struct TargetPriorityOption
    {
        public TargetPriority Value;
    }

    [Serializable]
    public struct LocalPositioningOption
    {
        public LocalPositioning Value;
    }

    [Serializable]
    public struct SelfPreservationOption
    {
        public SelfPreservation Value;
    }

    [Serializable]
    public struct RoleGroupCatalogEntry
    {
        public RoleGroup RoleGroup;
        // 목록 순서 = 드롭다운 표시 순서 = override 초기값(첫 항목, Docs/설계/11번 §2.1).
        public List<TargetPriorityOption> TargetPriorityOptions;
        public List<LocalPositioningOption> PositioningOptions;
        public List<SelfPreservationOption> SelfPreservationOptions;
    }

    /// <summary>
    /// 역할군마다 허용되는 축별 후보(값만). 화면 표시용 한국어 라벨은 RoleGroupTacticsStringsTableAsset
    /// 조회로 분리됐다(Docs/설계/18번 §5/§6). 서포터/암살자는 대응 직업이 없어 항목을 비워둔다
    /// (Docs/기획/12번 §0) - TacticsCatalogValidator가 "직업은 매핑됐는데 후보가 빈 역할군"만 별도로
    /// 경고한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RoleGroupTacticsCatalog", menuName = "Game/Tactics/Role Group Tactics Catalog")]
    public class RoleGroupTacticsCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<RoleGroupCatalogEntry> entries = new();

        public bool TryGetEntry(RoleGroup roleGroup, out RoleGroupCatalogEntry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate.RoleGroup == roleGroup)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public IReadOnlyList<RoleGroupCatalogEntry> Entries => entries;
    }
}
