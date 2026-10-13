using System.Collections.Generic;

namespace Game.Core
{
    public enum TripCheckKind { Battle, Dialogue, Event }

    /// <summary>
    /// 판정 시점 계산(설계 87 §6.3). 전투 0초 지점(5초마다), 대화 +1.25초(20초마다), 이벤트 +2.5초(5초마다) - 세 판정이 같은 순간에 겹치지 않게
    /// 한 시간 축에서 어긋나게 둔다(기획 86 §3.2). 한 번에 여러 경계를 넘으면 시각 순서대로 모두 낸다.
    /// </summary>
    public sealed class TripCheckSchedule
    {
        public const float BattleIntervalSeconds = 5f;
        public const float DialogueOffsetSeconds = 1.25f;
        public const float DialogueIntervalSeconds = 20f;
        public const float EventOffsetSeconds = 2.5f;
        public const float EventIntervalSeconds = 5f;

        private float nextBattle;
        private float nextDialogue;
        private float nextEvent;

        public float Elapsed { get; private set; }

        public TripCheckSchedule() => Reset();

        public void Reset()
        {
            Elapsed = 0f;
            nextBattle = BattleIntervalSeconds;
            nextDialogue = DialogueOffsetSeconds;
            nextEvent = EventOffsetSeconds;
        }

        public float SecondsUntil(TripCheckKind kind) => NextOf(kind) - Elapsed;

        public void Advance(float delta, List<TripCheckKind> fired)
        {
            fired.Clear();
            if (delta <= 0f) return;
            Elapsed += delta;

            while (true)
            {
                var kind = Earliest();
                if (NextOf(kind) > Elapsed + 1e-5f) break;
                fired.Add(kind);
                switch (kind)
                {
                    case TripCheckKind.Battle: nextBattle += BattleIntervalSeconds; break;
                    case TripCheckKind.Dialogue: nextDialogue += DialogueIntervalSeconds; break;
                    default: nextEvent += EventIntervalSeconds; break;
                }
            }
        }

        private float NextOf(TripCheckKind kind) => kind switch
        {
            TripCheckKind.Battle => nextBattle,
            TripCheckKind.Dialogue => nextDialogue,
            _ => nextEvent,
        };

        // 오프셋 설계상 같은 시각은 나오지 않지만, 같으면 전투 → 대화 → 이벤트 순.
        private TripCheckKind Earliest()
        {
            var kind = TripCheckKind.Battle;
            if (nextDialogue < NextOf(kind)) kind = TripCheckKind.Dialogue;
            if (nextEvent < NextOf(kind)) kind = TripCheckKind.Event;
            return kind;
        }
    }
}
