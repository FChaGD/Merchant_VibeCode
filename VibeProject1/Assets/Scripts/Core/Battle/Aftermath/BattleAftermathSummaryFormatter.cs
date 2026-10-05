using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 정산 요약 → 결과 팝업 상세 문구(설계 79번 §6.1, 기획 78번 §4-7). 0인 항목·줄은 생략한다 - 아무 일도 없던 전투에 "파손 0" 같은 줄을
    /// 늘어놓지 않기 위해서다. 전부 0이면 string.Empty(팝업이 상세 라벨을 숨긴다).
    /// </summary>
    public static class BattleAftermathSummaryFormatter
    {
        public static string Format(BattleAftermathSummary s)
        {
            var lines = new List<string>(4);

            var cargo = new List<string>(4);
            if (s.Broken > 0) cargo.Add($"파손 {s.Broken}");
            if (s.StolenLost > 0) cargo.Add($"도난 손실 {s.StolenLost}");
            if (s.Recoverable > 0) cargo.Add($"회수 대상 {s.Recoverable}");
            if (s.DefeatLost > 0) cargo.Add($"패배 손실 {s.DefeatLost}");
            if (cargo.Count > 0) lines.Add("화물: " + string.Join(" · ", cargo));

            if (s.DestroyedWagons > 0) lines.Add($"마차: 파괴 {s.DestroyedWagons}대(보유 목록에서 제거)");

            var formation = new List<string>(2);
            // 재배치 수는 표시하지 않는다(2026-10-05 사용자 결정, 기획 78번 §4-7 개정) - 팔레트 복귀만 알린다.
            if (s.Released > 0) formation.Add($"팔레트 복귀 {s.Released}");
            if (formation.Count > 0) lines.Add("대열: " + string.Join(" · ", formation));

            var seconds = (int)Math.Round(s.ExtraSeconds);
            if (seconds > 0) lines.Add($"상행 시간 +{seconds}초");

            return lines.Count > 0 ? string.Join("\n", lines) : string.Empty;
        }
    }
}
