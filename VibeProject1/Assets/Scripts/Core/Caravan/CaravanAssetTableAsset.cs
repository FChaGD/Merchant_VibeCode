using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct CaravanAssetEntry
    {
        public string Id; // 개체 Id(예: Wagon01) - 한 행 = 마차 한 대 / 시설 하나(Docs/기획/55번 §3)
        public int Price;
        // 대열 영역 모양 마스크(Docs/기획/65번 §3.1 - 0/1/2, 2 = 기준 칸). 파싱은 카탈로그가 FormationAreaShape로 한다.
        public string AreaShape;
        // 적재 공간 모양 마스크(Docs/기획/63번 §3.1) - 마차만 쓰고 시설은 빈 문자열. 파싱은 카탈로그가 InventoryShape로 한다.
        public string CargoShape;
        // 최대 내구도(Docs/기획/90번 §3-4) - 1 이상 정수, 임포터가 막는다. 전투 시작 최대치·수리비 계산의 기준이다.
        public int MaxDurability;
    }

    /// <summary>
    /// 마차·시설 개체 데이터(Docs/설계/56번 §2.2). 두 종류의 열 구성이 같아(Id·가격·대열 모양·최대 내구도) 클래스 하나를 경로만 달리해 공유한다 -
    /// 시설 스탯처럼 종류별로 열이 갈라지면 그때 분리한다. 값은 CaravanAssetTableImporter가 Assets/Table/Caravan/에서 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "CaravanAssetTable", menuName = "Game/Table/Caravan Asset Table")]
    public class CaravanAssetTableAsset : ScriptableObject
    {
        [SerializeField] private List<CaravanAssetEntry> entries = new();

        public IReadOnlyList<CaravanAssetEntry> Entries => entries;
    }
}
