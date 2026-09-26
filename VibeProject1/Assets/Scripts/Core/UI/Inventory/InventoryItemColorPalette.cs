using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 품목(Id)별 표시 색(Docs/설계/40번 §5.6). 아이콘이 없는 동안의 임시 식별 수단이라 테이블에 색을 두지 않고
    /// Id의 고정 해시로 팔레트에서 고른다. string.GetHashCode는 실행마다 값이 달라질 수 있어 FNV-1a를 쓴다.
    /// 품목이 팔레트 수를 넘으면 색이 겹칠 수 있다 - 아이콘 도입 시 대체 대상.
    /// </summary>
    public static class InventoryItemColorPalette
    {
        private static readonly Color[] Palette =
        {
            new(0.95f, 0.62f, 0.55f, 1f),
            new(0.98f, 0.80f, 0.45f, 1f),
            new(0.72f, 0.88f, 0.52f, 1f),
            new(0.50f, 0.83f, 0.78f, 1f),
            new(0.55f, 0.72f, 0.95f, 1f),
            new(0.75f, 0.62f, 0.92f, 1f),
            new(0.93f, 0.60f, 0.80f, 1f),
            new(0.78f, 0.72f, 0.60f, 1f),
        };

        public static Color ColorFor(string itemId) => Palette[IndexFor(itemId)];

        public static int IndexFor(string itemId) => (int)(Fnv1a(itemId ?? string.Empty) % (uint)Palette.Length);

        private static uint Fnv1a(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
