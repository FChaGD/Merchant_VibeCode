namespace Game.Core
{
    /// <summary>
    /// 고용 캐릭터 제거 전용 계약(Docs/설계/81번 §6.3). 상행 종료 처리가 사망자를 상단에서 뺄 때만 쓴다 - 고용 화면 계약(IHiredCharacterRoster)에
    /// 제거 권한을 넓히지 않기 위해 따로 둔다(IOwnedCaravanAssetRemover와 같은 이유). 구현체는 이 타입으로 따로 DI 등록한다.
    /// </summary>
    public interface IHiredCharacterRemover
    {
        bool TryRemoveHired(string characterId);
    }
}
