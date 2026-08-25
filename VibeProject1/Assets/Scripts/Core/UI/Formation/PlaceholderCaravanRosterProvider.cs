using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트를 테스트하기 위한 임시 로스터 제공자.
    /// 용병 3직업(전사=사각형/궁수=오각형/방패병=육각형)·마차(삼각형)·시설(원형) 1개씩 고정 제공한다 -
    /// 아이콘 모양으로 직업을 구분해 배치할 수 있게 해, 전투 쪽(LiveBattleSimulationRule)이 배치된
    /// 유닛 Id로 실제 직업을 조회할 수 있다. 실제 데이터 시스템이 생기면 대체된다.
    /// </summary>
    public class PlaceholderCaravanRosterProvider : MonoBehaviour, ICaravanRosterProvider, IManagedComponent
    {
        [SerializeField] private Sprite warriorIcon;
        [SerializeField] private Sprite archerIcon;
        [SerializeField] private Sprite shieldBearerIcon;
        [SerializeField] private Sprite wagonIcon;
        [SerializeField] private Sprite facilityIcon;

        private List<IFormationUnit> roster;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ICaravanRosterProvider>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            roster = new List<IFormationUnit>
            {
                new PlaceholderMercenaryUnit("character-warrior", "전사", warriorIcon, MercenaryClass.Warrior),
                new PlaceholderMercenaryUnit("character-archer", "궁수", archerIcon, MercenaryClass.Archer),
                new PlaceholderMercenaryUnit("character-shieldbearer", "방패병", shieldBearerIcon, MercenaryClass.ShieldBearer),
                new PlaceholderFormationUnit("wagon-01", "마차", wagonIcon, FormationUnitKind.Wagon),
                new PlaceholderFormationUnit("facility-01", "시설", facilityIcon, FormationUnitKind.Facility),
            };
        }

        public IReadOnlyList<IFormationUnit> GetRoster() => roster;
    }
}
