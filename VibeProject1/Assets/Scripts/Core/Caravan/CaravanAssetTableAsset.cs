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
    }

    /// <summary>
    /// 마차·시설 개체 데이터(Docs/설계/56번 §2.2). 두 종류의 열 구성이 같아(Id·가격) 클래스 하나를 경로만 달리해 공유한다 -
    /// 시설 스탯처럼 종류별로 열이 갈라지면 그때 분리한다. 값은 CaravanAssetTableImporter가 Assets/Table/Caravan/에서 채운다.
    /// </summary>
    [CreateAssetMenu(fileName = "CaravanAssetTable", menuName = "Game/Table/Caravan Asset Table")]
    public class CaravanAssetTableAsset : ScriptableObject
    {
        [SerializeField] private List<CaravanAssetEntry> entries = new();

        public IReadOnlyList<CaravanAssetEntry> Entries => entries;
    }
}
