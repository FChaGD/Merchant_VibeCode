using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct TripRegionStringEntry
    {
        public int Id;
        public string Name;
    }

    /// <summary>
    /// 지역 Id → 이름(Docs/설계/69번 §3.3). City.xlsx RegionStrings 시트의 컴파일된 형태 - 데이터(지도 자산)와 표시 문자열을
    /// 나누는 테이블 관례(기획 14번 §6.1)를 도시 스트링(TripCityStringsTableAsset)과 같게 따른다.
    /// </summary>
    [CreateAssetMenu(fileName = "TripRegionStringsTable", menuName = "Game/Trip/Trip Region Strings Table")]
    public class TripRegionStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<TripRegionStringEntry> entries = new();

        public IReadOnlyList<TripRegionStringEntry> Entries => entries;
    }
}
