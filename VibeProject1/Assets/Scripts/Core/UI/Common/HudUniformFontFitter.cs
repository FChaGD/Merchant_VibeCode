using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 한 줄에 놓인 여러 레이블의 공통 글자 크기. TMP 자동 크기는 레이블마다 크기가 달라져 한 줄이 들쭉날쭉하므로,
    /// 최대 크기 기준 필요 폭의 합이 남은 폭을 넘을 때만 같은 비율로 줄인다(Docs/설계/101번 §3·§4.2).
    /// 최소 크기에서도 넘치면 최소 크기로 넘친 채 둔다.
    /// </summary>
    public static class HudUniformFontFitter
    {
        public static float Fit(IReadOnlyList<float> requiredWidthsAtMax, float availableWidth, float maxSize, float minSize)
        {
            var required = 0f;
            if (requiredWidthsAtMax != null)
            {
                foreach (var width in requiredWidthsAtMax) required += Mathf.Max(0f, width);
            }

            if (required <= 0f) return maxSize;
            if (availableWidth <= 0f) return minSize;
            return Mathf.Clamp(maxSize * availableWidth / required, minSize, maxSize);
        }
    }
}
