using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 활성 규칙이 가리키는 레이어의 합집합(Docs/Refactor/2026-10-26_공통.md B-4). 같은 레이어를 여러 규칙(모달·시설 화면)이 가릴 때
    /// 하나만 풀려도 나머지가 참이면 계속 숨어야 해서, 규칙마다 따로 켜고 끄지 않고 매번 전체를 다시 계산한다.
    /// </summary>
    public static class LayerHideResolver
    {
        public static HashSet<T> HiddenLayers<T>(IReadOnlyList<(bool active, IReadOnlyList<T> layers)> rules)
        {
            var hidden = new HashSet<T>();
            foreach (var (active, layers) in rules)
            {
                if (!active || layers == null) continue;
                foreach (var layer in layers) hidden.Add(layer);
            }
            return hidden;
        }
    }
}
