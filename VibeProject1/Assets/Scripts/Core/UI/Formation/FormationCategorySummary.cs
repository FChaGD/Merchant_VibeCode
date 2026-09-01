using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정비창 팔레트가 카테고리 한 줄에 표시할 요약(설계 16번) - FormationPanel.BuildCategorySummaries가
    /// 로스터+현재 배치+사망 상태를 조합해 매번 새로 계산한다.
    /// </summary>
    public sealed class FormationCategorySummary
    {
        public FormationCategoryKey Key { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public int TotalCount { get; }

        /// <summary>사망하지 않았고 현재 어느 슬롯에도 배치되지 않은 개체 수.</summary>
        public int AvailableCount { get; }

        public FormationCategorySummary(FormationCategoryKey key, string displayName, Sprite icon, int totalCount, int availableCount)
        {
            Key = key;
            DisplayName = displayName;
            Icon = icon;
            TotalCount = totalCount;
            AvailableCount = availableCount;
        }
    }
}
