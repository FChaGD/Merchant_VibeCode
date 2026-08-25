using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트의 직업별 용병 테스트 데이터로 쓴다.
    /// PlaceholderFormationUnit과 달리 MercenaryClass를 갖는다(IMercenaryUnit) - 정비창에서 아이콘
    /// 모양(사각형/오각형/육각형)으로 직업을 구분해 배치할 수 있게 한다. 실제 캐릭터 데이터 모델이
    /// 생기면 대체된다.
    /// </summary>
    public class PlaceholderMercenaryUnit : IMercenaryUnit
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public FormationUnitKind Kind => FormationUnitKind.Character;
        public MercenaryClass Class { get; }

        public PlaceholderMercenaryUnit(string id, string displayName, Sprite icon, MercenaryClass mercenaryClass)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
            Class = mercenaryClass;
        }
    }
}
