namespace Game.Core
{
    /// <summary>
    /// 섹션 조회 편의 메서드(Docs/설계/64번 §4.1). 섹션 목록이 짧아(마차 수) 선형 검색으로 충분하다.
    /// </summary>
    public static class InventoryReaderExtensions
    {
        public static bool TryGetSection(this IInventoryReader reader, string sectionId, out InventorySection section)
        {
            var sections = reader.Sections;
            for (var i = 0; i < sections.Count; i++)
            {
                if (sectionId == null ? i == 0 : sections[i].Id == sectionId)
                {
                    section = sections[i];
                    return true;
                }
            }

            section = null;
            return false;
        }

        public static int IndexOfSection(this IInventoryReader reader, string sectionId)
        {
            var sections = reader.Sections;
            for (var i = 0; i < sections.Count; i++)
            {
                if (sections[i].Id == sectionId) return i;
            }
            return -1;
        }
    }
}
