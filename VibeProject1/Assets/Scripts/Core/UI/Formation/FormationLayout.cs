namespace Game.Core
{
    /// <summary>
    /// 슬롯 인덱스별로 배치된 유닛 Id와 그리드 모양(열/행 수)을 함께 보관한다. 빈 슬롯은 null.
    /// 그리드 모양을 데이터에 포함시킨 이유: 배치 UI 화면 요소는 콘텐츠 씬(Hub/Field 등)마다 별도
    /// 인스턴스라 각자 다른 열/행 수(FormationGridView의 씬별 값)를 가질 수 있었다 - 한쪽에서 바꾼
    /// 크기가 다른 쪽에 반영되지 않아 배치가 잘려 보이는 문제가 있었다. 이제 저장된 FormationLayout이
    /// 그리드 모양의 기준이 되고, FormationPanel.Open()이 이 값으로 그리드를 다시 맞춘다.
    /// </summary>
    public class FormationLayout
    {
        private readonly string[] slotUnitIds;

        public int ColumnCount { get; }
        public int RowCount { get; }
        public int SlotCount => slotUnitIds.Length;

        public FormationLayout(int columnCount, int rowCount)
        {
            ColumnCount = columnCount;
            RowCount = rowCount;
            slotUnitIds = new string[columnCount * rowCount];
        }

        private FormationLayout(int columnCount, int rowCount, string[] slotUnitIds)
        {
            ColumnCount = columnCount;
            RowCount = rowCount;
            this.slotUnitIds = slotUnitIds;
        }

        public string GetUnitId(int slotIndex) => slotUnitIds[slotIndex];

        public void SetUnitId(int slotIndex, string unitId) => slotUnitIds[slotIndex] = unitId;

        public void Clear(int slotIndex) => slotUnitIds[slotIndex] = null;

        public void Swap(int slotIndexA, int slotIndexB)
        {
            (slotUnitIds[slotIndexA], slotUnitIds[slotIndexB]) = (slotUnitIds[slotIndexB], slotUnitIds[slotIndexA]);
        }

        public FormationLayout Clone() => new(ColumnCount, RowCount, (string[])slotUnitIds.Clone());
    }
}
