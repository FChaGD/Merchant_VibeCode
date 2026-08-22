using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트를 테스트하기 위한 임시 로스터 제공자.
    /// 캐릭터(사각형)/마차(삼각형)/시설(원형) 1개씩 고정 제공한다. 실제 데이터 시스템이 생기면 대체된다.
    /// </summary>
    public class PlaceholderCaravanRosterProvider : MonoBehaviour, ICaravanRosterProvider, IManagedComponent
    {
        [SerializeField] private Sprite characterIcon;
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
                new PlaceholderFormationUnit("character-01", "캐릭터", characterIcon, FormationUnitKind.Character),
                new PlaceholderFormationUnit("wagon-01", "마차", wagonIcon, FormationUnitKind.Wagon),
                new PlaceholderFormationUnit("facility-01", "시설", facilityIcon, FormationUnitKind.Facility),
            };
        }

        public IReadOnlyList<IFormationUnit> GetRoster() => roster;
    }
}
