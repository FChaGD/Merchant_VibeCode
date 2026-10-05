namespace Game.Core
{
    /// <summary>
    /// 보유 마차·시설 제거 전용 계약(Docs/설계/79번 §5.2). 전투에서 파괴된 마차를 보유 목록에서 빼는 정산만 쓴다 - 마구간 구매
    /// 소비자(IOwnedCaravanAssetRoster)에 제거 권한을 넓히지 않기 위해 따로 두었다(ISP). 구현체는 이 타입으로 따로 DI 등록한다.
    /// </summary>
    public interface IOwnedCaravanAssetRemover
    {
        /// <summary>보유 중인 마차·시설이면 제거하고 true. 캐릭터·미보유·알 수 없는 Id면 false.</summary>
        bool TryRemoveOwned(string id);
    }
}
