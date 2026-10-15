namespace Game.Core
{
    /// <summary>
    /// 전투 종료 시점의 마차·시설 한 기(Docs/설계/91번 §4.2). 전투 중에는 내구도 저장소를 건드리지 않고 결과에 실어 보낸다 - 화물 보고서와 같이
    /// 정산이 결과 판정 뒤 한 번에 반영한다(설계 79번 §15-2). UnitId는 보유 개체 Id다.
    /// </summary>
    public readonly struct ProtectedUnitRecord
    {
        public string UnitId { get; }
        public ProtectedUnitKind Kind { get; }
        public float RemainingDurability { get; }
        public bool Destroyed { get; }

        public ProtectedUnitRecord(string unitId, ProtectedUnitKind kind, float remainingDurability, bool destroyed)
        {
            UnitId = unitId;
            Kind = kind;
            RemainingDurability = remainingDurability;
            Destroyed = destroyed;
        }
    }
}
