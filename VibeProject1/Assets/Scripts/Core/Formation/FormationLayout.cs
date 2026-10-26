namespace Game.Core
{
    /// <summary>
    /// 슬롯 인덱스별로 배치된 유닛 Id와 외곽 판 모양(열/행 수)을 함께 보관한다. 빈 슬롯은 null.
    /// 판 모양을 데이터에 포함시킨 이유: 배치 UI 화면 요소는 콘텐츠 씬(Hub/Field 등)마다 별도
    /// 인스턴스라 각자 다른 열/행 수를 가질 수 있었다 - 한쪽에서 바꾼 크기가 다른 쪽에 반영되지 않아
    /// 배치가 잘려 보이는 문제가 있었다. 이제 저장된 FormationLayout이 판 모양의 기준이다.
    /// 판 전체가 대열인 것은 아니다 - 대열(배치 가능한 칸)은 마차·시설 위치로 FormationArea가 계산한다(Docs/기획/59번).
    /// </summary>
    public class FormationLayout
    {
        // 외곽 판 크기(Docs/기획/59번 §4.3 - 고정 50 × 50)의 단일 출처 - 배치 UI, LiveBattleSimulationRule,
        // InMemoryFieldFormationActivityRepository의 배치 없음 폴백이 이 값을 공유한다. 따로 들고 있으면 하나만 바뀌었을 때
        // 조용히 어긋난다(BattleFieldGeometry와 같은 이유).
        public const int DefaultColumnCount = 50;
        public const int DefaultRowCount = 50;

        private readonly string[] slotUnitIds;

        public int ColumnCount { get; }
        public int RowCount { get; }
        public int SlotCount => slotUnitIds.Length;

        /// <summary>마차가 없을 때 대열이 되는 기준 칸 - 판 중앙(짝수 판이면 중앙 두 칸 중 앞쪽, 50 × 50이면 (24, 24)).</summary>
        public int AnchorSlotIndex => (RowCount - 1) / 2 * ColumnCount + (ColumnCount - 1) / 2;

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

        public static FormationLayout CreateDefault() => new(DefaultColumnCount, DefaultRowCount);

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
