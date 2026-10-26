using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct TripCityStringEntry
    {
        public int Id;
        public string Name;
        public string Description;
    }

    /// <summary>
    /// 도시 Id→이름/설명(Docs/기획/15번 §8.3, 설계 20번 §9.4) - 엑셀에서 사람이 직접 채우는 String
    /// 시트(`TripCityStrings`)의 컴파일된 형태. 입력 UI는 만들지 않는다(기획 §8.1) - 좌표(Data,
    /// TripCityMapAsset)와 분리한 이유는 다른 데이터 테이블과 같다(기획 14번 §6.1). `LocalizedStringEntry`
    /// (Id+Ko 단일 텍스트, 데이터 테이블 v2용)를 재사용하지 않는다 - 도시는 ITripLocationInfo가 이미
    /// DisplayName/Description 두 필드를 요구해서 모양이 다르다.
    /// </summary>
    [CreateAssetMenu(fileName = "TripCityStringsTable", menuName = "Game/Trip/Trip City Strings Table")]
    public class TripCityStringsTableAsset : ScriptableObject
    {
        [SerializeField] private List<TripCityStringEntry> entries = new();

        public bool TryGetEntry(int cityId, out TripCityStringEntry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate.Id == cityId)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
