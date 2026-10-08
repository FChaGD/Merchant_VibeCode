using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct TownStockEntry
    {
        public int CityId;
        public TownStockCategory Category;
        public string ItemId;
        public int Quantity;
    }

    /// <summary>
    /// 마을-구매아이템 관계(Docs/기획/80번 §3-8). 행 순서 = 화면 표시 순서. 값은 TownStockTableImporter가 Assets/Table/Town/TownStock.xlsx에서
    /// 채운다 - 엑셀이 단일 진실 소스라 인스펙터 수정은 다음 임포트에 덮어써진다.
    /// </summary>
    [CreateAssetMenu(fileName = "TownStockTable", menuName = "Game/Table/Town Stock Table")]
    public class TownStockTableAsset : ScriptableObject
    {
        [SerializeField] private List<TownStockEntry> entries = new();

        public IReadOnlyList<TownStockEntry> Entries => entries;
    }
}
