namespace Game.Core
{
    /// <summary>
    /// 전투 중 물품 하나의 최종 상태(설계 79번 §3.2). 원장은 기록만 하고 해석(손실·회수)은 전투 뒤 정산이 결과에 따라
    /// 정한다 - 예를 들어 Stolen(소유 적이 전투 끝까지 살아 있음)·Unprotected는 승리면 회수, 패배면 손실이다.
    /// </summary>
    public enum CargoItemFate
    {
        // 그대로 마차에 있음.
        Intact,
        // 사라짐(피격·파괴 시 파손).
        Broken,
        // 적이 가져가 그 적이 소유 중(아직 사망·도주하지 않음).
        Stolen,
        // 파괴된 마차의 잔해에 놓임 - 적이 잔해를 공격해 가져갈 수 있다.
        Unprotected,
        // 가져간 적이 사망 - 승리 시 회수 대상.
        Recoverable,
        // 가져간 적이 도주 - 되찾을 수 없음.
        StolenLost
    }

    /// <summary>전투 결과 보고서의 물품 한 줄. WagonId는 전투 시작 시 실려 있던 마차(= 화물 섹션 Id, 설계 64번 §3.2).</summary>
    public readonly struct CargoItemRecord
    {
        public string InstanceId { get; }
        public string WagonId { get; }
        public IInventoryItemDefinition Definition { get; }
        public CargoItemFate Fate { get; }

        public CargoItemRecord(string instanceId, string wagonId, IInventoryItemDefinition definition, CargoItemFate fate)
        {
            InstanceId = instanceId;
            WagonId = wagonId;
            Definition = definition;
            Fate = fate;
        }
    }
}
