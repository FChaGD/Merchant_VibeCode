namespace Game.Core
{
    /// <summary>
    /// 앞말의 받침에 따라 조사를 고른다(Docs/Refactor/2026-10-26_씬전용.md I-2, SE-7) - 이름이 바뀌는 문구에 조사를 고정하면 "마차을"처럼 틀린다.
    /// 끝 글자가 한글 음절이면 받침 유무로, 숫자면 한국어 읽기(영·일·이·삼·사·오·육·칠·팔·구)의 받침으로 고른다. 그 밖(영문·기호·빈 문자열)은
    /// 판단할 수 없어 "을(를)"처럼 둘 다 적는다. 조사만 돌려준다 - 호출부가 앞말에 붙인다.
    /// </summary>
    public static class KoreanParticle
    {
        private const char HangulFirst = '가';
        private const char HangulLast = '힣';
        // 0~9 읽기의 받침 유무(영·일·삼·육·칠·팔 = 받침 있음).
        private static readonly bool[] DigitHasBatchim = { true, true, false, true, false, false, true, true, true, false };

        public static string EulReul(string word) => Choose(word, "을", "를");
        public static string IGa(string word) => Choose(word, "이", "가");
        public static string EunNeun(string word) => Choose(word, "은", "는");

        private static string Choose(string word, string withBatchim, string withoutBatchim)
        {
            var batchim = HasBatchim(word);
            if (batchim == null) return $"{withBatchim}({withoutBatchim})";
            return batchim.Value ? withBatchim : withoutBatchim;
        }

        private static bool? HasBatchim(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            var last = word[word.Length - 1];
            if (last >= HangulFirst && last <= HangulLast) return (last - HangulFirst) % 28 != 0;
            if (last >= '0' && last <= '9') return DigitHasBatchim[last - '0'];
            return null;
        }
    }
}
