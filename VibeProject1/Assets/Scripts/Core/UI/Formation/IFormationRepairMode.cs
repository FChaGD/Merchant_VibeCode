using System;

namespace Game.Core
{
    /// <summary>
    /// 전투 후 끊어진 대열을 잇는 정비창 정리 모드(설계 79번 §8). 결과 정리 플로우 단계가 정비창을 UIManager로 연 뒤 호출한다.
    /// 패널은 [완료] 시 onCompleted만 부르고 스스로 닫지 않는다 - 닫기는 UI 패널 규칙대로 호출자가 UIManager.Close로 한다.
    /// 정리 모드 화면 요소가 없으면 플로우가 멈추지 않도록 onCompleted를 곧바로 부른다.
    /// </summary>
    public interface IFormationRepairMode
    {
        // 정리 모드 화면 요소(안내 문구·[완료])가 바인딩돼 있는지 - 없으면 결과 정리 ③을 건너뛴다. 단계 적용 판정에 넣지 않으면 ① 버튼이
        // "다음"이 되고 정비창이 열렸다 바로 닫힌다(2026-10-05 검진 지적 4).
        bool CanRepair { get; }

        void BeginRepair(Action onCompleted);
    }
}
