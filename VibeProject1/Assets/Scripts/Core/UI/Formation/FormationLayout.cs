namespace Game.Core
{
    /// <summary>
    /// 슬롯 인덱스별로 배치된 유닛 Id를 보관한다. 빈 슬롯은 null.
    /// </summary>
    public class FormationLayout
    {
        private readonly string[] slotUnitIds;

        public int SlotCount => slotUnitIds.Length;

        public FormationLayout(int slotCount)
        {
            slotUnitIds = new string[slotCount];
        }

        private FormationLayout(string[] slotUnitIds)
        {
            this.slotUnitIds = slotUnitIds;
        }

        public string GetUnitId(int slotIndex) => slotUnitIds[slotIndex];

        public void SetUnitId(int slotIndex, string unitId) => slotUnitIds[slotIndex] = unitId;

        public void Clear(int slotIndex) => slotUnitIds[slotIndex] = null;

        public void Swap(int slotIndexA, int slotIndexB)
        {
            (slotUnitIds[slotIndexA], slotUnitIds[slotIndexB]) = (slotUnitIds[slotIndexB], slotUnitIds[slotIndexA]);
        }

        public FormationLayout Clone() => new((string[])slotUnitIds.Clone());
    }
}
