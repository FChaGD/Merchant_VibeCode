using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 아군 세팅 데이터 - 실제 게임의 IFormationRepository/FormationLayout(그리드
    /// 슬롯 기반)을 거치지 않는다. 이 씬은 정비창 대신 자유 드래그 팔레트로 전장에 직접 배치하므로
    /// "슬롯 좌표"가 아니라 "월드 좌표" 하나만 있으면 된다. 세팅 화면 미리보기와
    /// BattleTestSimulationRule.BuildSimulation()이 공용으로 읽는 단일 출처 - 전투 리셋은 이 목록을
    /// 지우지 않는다(세팅 상태로 복귀하는 것이지 세팅을 지우는 게 아님).
    /// Entry가 클래스인 이유(구조체가 아님) - 배치 취소(Id로 제거)/스탯 수치 조절(StatsOverride
    /// 갱신) 둘 다 "이미 목록에 들어간 항목을 나중에 식별해 변경"해야 해서, 값이 아니라 참조/Id
    /// 기반 식별이 필요하다(BattleTestUnitInfoPanelView 참고).
    /// </summary>
    public class BattleTestAllyRoster
    {
        public class Entry
        {
            public int Id { get; }
            public MercenaryClass Class { get; }
            public Vector2 Position { get; }
            // null이면 직업 기본 스탯(TableBattleUnitStatProvider) 그대로 사용 - 유닛 정보
            // 패널에서 값을 조절하면 여기에 채워진다.
            public BattleUnitStats? StatsOverride { get; set; }

            public Entry(int id, MercenaryClass unitClass, Vector2 position)
            {
                Id = id;
                Class = unitClass;
                Position = position;
            }
        }

        private readonly List<Entry> entries = new();
        private int nextId;

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Add(MercenaryClass unitClass, Vector2 position)
        {
            var entry = new Entry(nextId++, unitClass, position);
            entries.Add(entry);
            return entry;
        }

        public bool TryGet(int id, out Entry entry)
        {
            entry = entries.Find(e => e.Id == id);
            return entry != null;
        }

        public bool Remove(int id) => entries.RemoveAll(e => e.Id == id) > 0;

        public void Clear() => entries.Clear();
    }
}
