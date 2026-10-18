using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 효과 결과 줄 문구(설계 89 §5.1) - 한 곳에 모아 대본·테스트·화면이 같은 문구를 쓴다. 음수 부호는 U+2212(−) - TMP 한글 폰트에 없어 기호 대체 폰트가
    /// 처리한다(설계 70).
    /// </summary>
    public static class EventEffectFormat
    {
        private const string Minus = "−";
        public const string NoGoodsLost = "잃은 무역품 없음";
        public const string NoGoldLost = "잃은 골드 없음";

        public static string Gold(int signedAmount) => signedAmount >= 0 ? $"골드 +{signedAmount}" : $"골드 {Minus}{-signedAmount}";
        public static string GoodsGained(string name, int count) => $"{name} ×{count} 획득";
        public static string GoodsLost(string name, int count) => $"{name} ×{count} 잃음";
        public static string GoodsNoneLost(string name) => $"{name} — 잃은 것 없음";

        public static string GoodsLost(IReadOnlyList<GoodsLoss> losses)
        {
            if (losses == null || losses.Count == 0) return NoGoodsLost;
            var sb = new StringBuilder();
            for (var i = 0; i < losses.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(losses[i].Name).Append(" ×").Append(losses[i].Count);
            }
            return sb.Append(" 잃음").ToString();
        }

        public static string Delayed(float seconds) => $"이동 지연 {Seconds(seconds)}초";
        public static string Shortened(float seconds) => $"이동 단축 {Seconds(seconds)}초";

        /// <param name="scope">null = 영구(괄호 없음), "이번 구간", "3일".</param>
        public static string Difficulty(int change, string scope)
        {
            var sign = change >= 0 ? $"+{change}" : $"{Minus}{-change}";
            return scope == null ? $"도로 난이도 {sign}" : $"도로 난이도 {sign} ({scope})";
        }

        public static string Joined(string name) => $"{name} 합류";
        public static string Left(string name) => $"{name} 이탈";

        private static string Seconds(float s) => s.ToString("0.#");
    }
}
