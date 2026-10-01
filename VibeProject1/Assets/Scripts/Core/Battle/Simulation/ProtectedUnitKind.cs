namespace Game.Core
{
    /// <summary>
    /// 전투 보호 대상의 종류(Docs/설계/74번 §2.1). 패배 조건이 마차(전부 파괴)와 시설(캐릭터와 함께 전투 가능 아군)을 따로
    /// 센다. 정비창 종류(FormationUnitKind)를 그대로 쓰지 않는 이유 - Character가 섞여 있어 보호 대상에 맞지 않는 값이 생긴다.
    /// </summary>
    public enum ProtectedUnitKind
    {
        Wagon,
        Facility,
    }
}
