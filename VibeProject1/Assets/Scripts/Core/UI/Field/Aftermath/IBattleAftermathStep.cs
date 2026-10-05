using System;

namespace Game.Core
{
    /// <summary>
    /// 결과 정리 플로우의 한 단계(설계 79번 §6). 해당 없는 단계는 AppliesTo가 false를 돌려 건너뛴다(기획 78번 §4-2). ① 결과 팝업이
    /// 버튼 문구("다음"/마무리 문구)를 정할 때도 뒤 단계의 AppliesTo를 보므로, 실행할 수 없는 단계(화면 요소 없음 등)도 여기서 false여야 한다.
    /// </summary>
    internal interface IBattleAftermathStep
    {
        bool AppliesTo(BattleAftermathContext context);

        /// <summary>단계가 끝나면 onDone을 부른다.</summary>
        void Run(BattleAftermathContext context, Action onDone);
    }
}
