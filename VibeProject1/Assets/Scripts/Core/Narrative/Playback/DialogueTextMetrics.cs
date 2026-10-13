namespace Game.Core
{
    /// <summary>
    /// 글자 수 기준은 공백 제외·문장부호 포함(기획 86 §4.5). 공백은 시간을 쓰지 않으므로, k번째 글자가 찍히는 순간 뒤따르는 공백까지 함께 보인다.
    /// </summary>
    public static class DialogueTextMetrics
    {
        public static int CountTyped(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var count = 0;
            foreach (var c in text) if (!char.IsWhiteSpace(c)) count++;
            return count;
        }

        /// <summary>typedCount개 글자가 찍혔을 때 보이는 앞부분 길이(뒤따르는 공백 포함).</summary>
        public static int VisibleLengthFor(string text, int typedCount)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var seen = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i])) continue;
                if (seen == typedCount) return i;
                seen++;
            }
            return text.Length;
        }
    }
}
