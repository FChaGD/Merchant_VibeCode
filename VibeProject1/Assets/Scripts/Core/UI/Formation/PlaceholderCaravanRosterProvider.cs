using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트를 테스트하기 위한 임시 로스터 제공자.
    /// 유닛 배치 상한 기획(11번) §3 확정대로 카테고리(용병 3직업+마차+시설)당 5개씩 개별 인스턴스(고유
    /// Id)를 제공한다 - 같은 카테고리 내 인스턴스는 아이콘/직업이 완전히 동일하고(재고 수량 의미),
    /// 실제로 서로 달라지는 건 전투 결과로 갈리는 HP뿐이다(설계 15번, IUnitConditionRepository가 별도
    /// 관리). 실제 데이터 시스템이 생기면 대체된다.
    /// </summary>
    public class PlaceholderCaravanRosterProvider : MonoBehaviour, ICaravanRosterProvider, IManagedComponent
    {
        // 기획 11번 §2 확정값 - 카테고리당 최대 5개.
        private const int InstancesPerCategory = 5;

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
            roster = new List<IFormationUnit>();
            AddMercenaryInstances(roster, "character-warrior", "전사", warriorIcon, MercenaryClass.Warrior);
            AddMercenaryInstances(roster, "character-archer", "궁수", archerIcon, MercenaryClass.Archer);
            AddMercenaryInstances(roster, "character-shieldbearer", "방패병", shieldBearerIcon, MercenaryClass.ShieldBearer);
            AddFormationInstances(roster, "wagon", "마차", wagonIcon, FormationUnitKind.Wagon);
            AddFormationInstances(roster, "facility", "시설", facilityIcon, FormationUnitKind.Facility);
        }

        public IReadOnlyList<IFormationUnit> GetRoster() => roster;

        private static void AddMercenaryInstances(List<IFormationUnit> roster, string idPrefix, string displayName, Sprite icon, MercenaryClass mercenaryClass)
        {
            for (var i = 1; i <= InstancesPerCategory; i++)
            {
                roster.Add(new PlaceholderMercenaryUnit($"{idPrefix}-{i}", displayName, icon, mercenaryClass));
            }
        }

        private static void AddFormationInstances(List<IFormationUnit> roster, string idPrefix, string displayName, Sprite icon, FormationUnitKind kind)
        {
            for (var i = 1; i <= InstancesPerCategory; i++)
            {
                roster.Add(new PlaceholderFormationUnit($"{idPrefix}-{i}", displayName, icon, kind));
            }
        }
    }
}
