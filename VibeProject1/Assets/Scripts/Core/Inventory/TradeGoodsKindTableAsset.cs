using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 교역품의 일반/특산 구분(Docs/설계/50번 §4.1). 교역품에만 있는 속성이라 카테고리 공용 구조체인
    /// ItemDefinitionEntry에 넣지 않고 별도 자산으로 둔다 - 판매 목록 제공자만 읽는다.
    /// </summary>
    [CreateAssetMenu(fileName = "TradeGoodsKindTable", menuName = "Game/Table/Trade Goods Kind Table")]
    public class TradeGoodsKindTableAsset : ScriptableObject
    {
        [SerializeField] private List<TradeGoodsKindEntry> entries = new();

        private Dictionary<string, TradeGoodsKind> lookup;

        public bool TryGetKind(string id, out TradeGoodsKind kind)
        {
            EnsureLookupBuilt();
            return lookup.TryGetValue(id, out kind);
        }

        private void EnsureLookupBuilt()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, TradeGoodsKind>(entries.Count);
            foreach (var entry in entries)
            {
                lookup[entry.Id] = entry.Kind;
            }
        }
    }
}
