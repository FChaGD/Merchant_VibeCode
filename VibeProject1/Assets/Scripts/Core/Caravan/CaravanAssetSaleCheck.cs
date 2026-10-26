namespace Game.Core
{
    /// <summary>마구간 매각 판정 결과(Docs/설계/103번 §4.1).</summary>
    public enum CaravanAssetSaleCheck
    {
        Available,
        HasCargo,
        LastWagon,
        NotOwned,
    }
}
