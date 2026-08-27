namespace Game.Core
{
    /// <summary>
    /// 파티 전체 축 - 적 개체마다 개별로 판정된다(전체가 동시에 인식되지 않음, Docs/기획/12번 §2.1).
    /// </summary>
    public enum EnemyRecognitionType
    {
        // 스폰 후 1초 지연(기본값).
        OneSecondDelay,
        // 스폰 후 5초가 지나거나 그 전에 활동 반경 근처로 접근하면(둘 중 먼저) 인식.
        FiveSecondOrProximity,
        // 시간 제한 없음 - 활동 반경 근처로 접근하거나 아군을 공격하면(둘 중 먼저) 인식.
        // 피격으로 인한 인식은 공격한 그 개체만 인식된다.
        ProximityOrHit,
    }
}
