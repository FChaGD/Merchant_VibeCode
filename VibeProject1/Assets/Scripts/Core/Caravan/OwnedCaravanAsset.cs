namespace Game.Core
{
    /// <summary>보유 개체 하나의 조회 결과(Docs/설계/81번 §5.2). 번호는 조회 시점 기준이라 저장해 두면 재번호를 놓친다.</summary>
    public readonly struct OwnedCaravanAsset
    {
        public string InstanceId { get; }
        public CaravanAssetProfile Profile { get; }
        public int Number { get; }

        public OwnedCaravanAsset(string instanceId, CaravanAssetProfile profile, int number)
        {
            InstanceId = instanceId;
            Profile = profile;
            Number = number;
        }
    }

    /// <summary>개체 표시 이름 형식의 단일 소스(기획 80번 §4-1) - 마구간·적재 팝업·정비창이 같은 형식을 쓴다.</summary>
    public static class OwnedCaravanAssetNames
    {
        public static string Format(OwnedCaravanAsset asset) => $"{asset.Number}번 {asset.Profile.Name}";
    }
}
