using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 버리기 경고 문구(기획 95, Docs/설계/102번 §4.5). 같은 이름은 처음 나온 순서로 묶어 "이름 ×n"(1개면 ×1 생략)으로 적는다.
    /// 골드 상자는 버리면 적재 골드가 줄어드므로, 골드 정보가 있는 화면(상단 물류품)에서만 사라지는 골드를 한 줄 더 적는다.
    /// 진행 중 퀘스트의 지급 의뢰 화물이 섞여 있으면 퀘스트 이름과 경고 한 줄을 더 적는다(기획 106 §4.2, 설계 107번 §8.4) - 새 팝업 없이 같은 확인/취소를 쓴다.
    /// </summary>
    public static class InventoryDiscardWarning
    {
        public const string Header = "다음 아이템을 버립니다. 되돌릴 수 없습니다.";

        /// <summary>의뢰 화물 경고 줄(퀘스트 이름 목록). 해당 없으면 null - 출발 경고(TripDepartureWarnings)도 같은 줄을 쓴다.</summary>
        public static string QuestCargoLine(IReadOnlyList<string> questNames)
            => questNames == null || questNames.Count == 0 ? null : $"의뢰 화물입니다({string.Join(", ", questNames)}). 퀘스트 수행이 불가능해질 수 있습니다.";

        public static string Compose(IReadOnlyList<InventoryItemInstance> items, IGoldItemInfo goldInfo, IQuestCargoWarnings questWarnings = null)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            var goldBoxes = 0;
            foreach (var item in items)
            {
                var name = item.Definition?.DisplayName ?? PlaceholderText.Missing;
                if (counts.TryGetValue(name, out var count))
                {
                    counts[name] = count + 1;
                }
                else
                {
                    counts[name] = 1;
                    order.Add(name);
                }

                if (goldInfo != null && item.Definition != null && goldInfo.IsGoldItem(item.Definition.Id)) goldBoxes++;
            }

            var builder = new StringBuilder(Header).Append('\n');
            for (var i = 0; i < order.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(order[i]);
                if (counts[order[i]] > 1) builder.Append(" ×").Append(counts[order[i]]);
            }

            if (goldBoxes > 0)
            {
                builder.Append($"\n골드 상자 {goldBoxes}개(골드 {goldBoxes * goldInfo.GoldValuePerItem:N0})가 사라집니다.");
            }

            if (questWarnings != null && QuestCargoLine(questWarnings.Describe(ItemIds(items))) is { } questLine) builder.Append('\n').Append(questLine);
            return builder.ToString();
        }

        public static IEnumerable<string> ItemIds(IEnumerable<InventoryItemInstance> items)
        {
            foreach (var item in items)
            {
                if (item.Definition != null) yield return item.Definition.Id;
            }
        }
    }
}
