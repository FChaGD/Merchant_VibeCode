namespace Game.Core
{
    /// <summary>
    /// 전투 정산 반영 결과(설계 79번 §5). 결과 팝업 요약 줄(기획 78번 §4-7)과 정리 단계 건너뜀 판정이 이 값만 읽는다 - 단계들이 저장소를
    /// 다시 뒤져 반영 전후를 비교하지 않게 하기 위해서다. 포로·사망은 default(전부 0).
    /// </summary>
    public readonly struct BattleAftermathSummary
    {
        public int Broken { get; }
        public int StolenLost { get; }
        public int Recoverable { get; }
        public int DefeatLost { get; }
        public int DestroyedWagons { get; }
        public int Relocated { get; }
        public int Released { get; }
        public float ExtraSeconds { get; }
        /// <summary>정산 후 마차 덩어리가 2개 이상 - ③ 대열 정리 단계 대상.</summary>
        public bool FormationDisconnected { get; }

        public bool IsEmpty => Broken == 0 && StolenLost == 0 && Recoverable == 0 && DefeatLost == 0 && DestroyedWagons == 0
            && Relocated == 0 && Released == 0 && ExtraSeconds <= 0f && !FormationDisconnected;

        public BattleAftermathSummary(int broken, int stolenLost, int recoverable, int defeatLost, int destroyedWagons, int relocated, int released,
            float extraSeconds, bool formationDisconnected)
        {
            Broken = broken;
            StolenLost = stolenLost;
            Recoverable = recoverable;
            DefeatLost = defeatLost;
            DestroyedWagons = destroyedWagons;
            Relocated = relocated;
            Released = released;
            ExtraSeconds = extraSeconds;
            FormationDisconnected = formationDisconnected;
        }
    }
}
