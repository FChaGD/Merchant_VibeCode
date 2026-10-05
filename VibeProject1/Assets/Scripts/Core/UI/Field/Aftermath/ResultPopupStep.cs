using System;

namespace Game.Core
{
    /// <summary>
    /// ① 결과 팝업(설계 79번 §6.1). 제목·마무리 버튼 문구는 기존 결과별 문구를 그대로 쓰고, 상세에 정산 요약을 붙인다. 뒤에 실행될
    /// 단계가 있으면 버튼은 "다음"이다(기획 78번 §4-8). 뒤 단계 판단은 플로우가 갖고 있는데 플로우가 이 단계를 담고 있어 생성 순서가
    /// 순환하므로, 판단 함수를 생성 시 받는다.
    /// </summary>
    internal sealed class ResultPopupStep : IBattleAftermathStep
    {
        private const string NextButtonText = "다음";

        private readonly FieldResultPopupView popupView;
        private readonly Func<IBattleAftermathStep, BattleAftermathContext, bool> hasStepAfter;

        public ResultPopupStep(FieldResultPopupView popupView, Func<IBattleAftermathStep, BattleAftermathContext, bool> hasStepAfter)
        {
            this.popupView = popupView;
            this.hasStepAfter = hasStepAfter;
        }

        public bool AppliesTo(BattleAftermathContext context) => true;

        public void Run(BattleAftermathContext context, Action onDone)
        {
            var (title, finishButtonText) = GetTexts(context);
            var buttonText = hasStepAfter(this, context) ? NextButtonText : finishButtonText;
            var detail = BattleAftermathSummaryFormatter.Format(context.Summary);
            popupView.Show(title, detail, buttonText, onDone);
        }

        // 포로는 콘텐츠 미구현(테스트 단계)이라 기존처럼 사망과 같은 "플레이 종료"로 끝난다.
        private static (string title, string buttonText) GetTexts(BattleAftermathContext context)
        {
            if (context.IsVictory) return ("승리", "확인");

            return context.Consequence switch
            {
                DefeatConsequence.Death => ("패배 - 전멸", "플레이 종료"),
                DefeatConsequence.Flee => ("패배 - 도주", "상행 속행"),
                DefeatConsequence.Rout => ("패배 - 궤주", "귀환"),
                _ => ("패배 - 포로", "플레이 종료"),
            };
        }
    }
}
