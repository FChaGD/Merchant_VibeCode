using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트와 전투에 쓰는 임시 용병 개체. 캐릭터 테이블의 한 행(CharacterId)을
    /// 가리키고, 표시명(DisplayName)은 캐릭터 이름이 아니라 직업명이다 - 정비창 팔레트·정보 패널은 이름을 표시하지 않는다
    /// (Docs/기획/53번 §3.2). 실제 캐릭터 데이터 모델이 생기면 대체된다.
    /// </summary>
    public class PlaceholderMercenaryUnit : IMercenaryUnit
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public FormationUnitKind Kind => FormationUnitKind.Character;
        public string Class { get; }
        public string CharacterId { get; }

        public PlaceholderMercenaryUnit(string characterId, string displayName, Sprite icon, string mercenaryClass)
        {
            // 인스턴스 Id = 캐릭터 Id(설계 54번 §11) - 캐릭터는 테이블에서 유일하고 한 번만 고용되므로 충돌하지 않는다.
            Id = characterId;
            CharacterId = characterId;
            DisplayName = displayName;
            Icon = icon;
            Class = mercenaryClass;
        }
    }
}
