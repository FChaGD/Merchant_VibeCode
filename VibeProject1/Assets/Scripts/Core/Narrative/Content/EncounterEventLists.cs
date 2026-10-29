using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 발생 장소별 이벤트 목록(설계 109 §3.1). 상행 판정(5초)·휴식 점검(하루)마다 전체 목록을 거르지 않도록 등록 시 한 번 나눠 둔다.
    /// 테이블 순서를 유지한다 - 확정 이벤트 동시 성립의 보조 순서가 테이블 순서이기 때문이다.
    /// </summary>
    public sealed class EncounterEventLists
    {
        private readonly List<EventEntry> trip = new();
        private readonly List<EventEntry> rest = new();

        public EncounterEventLists(IReadOnlyList<EventEntry> all)
        {
            if (all == null) return;
            foreach (var e in all)
            {
                if (e == null) continue;
                (e.Place == EventPlace.Rest ? rest : trip).Add(e);
            }
        }

        public IReadOnlyList<EventEntry> At(EventPlace place) => place switch
        {
            EventPlace.Trip => trip,
            EventPlace.Rest => rest,
            _ => Array.Empty<EventEntry>(),
        };
    }
}
