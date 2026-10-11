using System;

namespace Game.Core
{
    /// <summary>
    /// 날짜 보관·진행(Docs/설계/85번 §3.1). DateManager가 감싸는 순수 객체 - Unity 없이 테스트하기 위해 분리했다.
    /// 실제로 바뀐 경우에만 알린다(0 이하 진행은 무시).
    /// </summary>
    public sealed class GameCalendar
    {
        public GameDate Today { get; private set; }

        public event Action Changed;

        public GameCalendar(int totalDays = 0)
        {
            Today = new GameDate(totalDays);
        }

        public void AdvanceDays(int days)
        {
            if (days <= 0) return;

            Today = new GameDate(Today.TotalDays + days);
            Changed?.Invoke();
        }
    }
}
