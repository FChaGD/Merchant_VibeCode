using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI에서 쓰는 임시 마차·시설 유닛. 대열 영역을 여는 반경(AreaRadius, Docs/기획/59번)을
    /// 마차·시설 테이블 값으로 받는다. 실제 마차/시설 데이터 모델이 생기면 대체된다.
    /// </summary>
    public class PlaceholderFormationUnit : IAreaAnchorUnit
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public FormationUnitKind Kind { get; }
        public int AreaRadius { get; }

        public PlaceholderFormationUnit(string id, string displayName, Sprite icon, FormationUnitKind kind, int areaRadius = 0)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
            Kind = kind;
            AreaRadius = areaRadius;
        }
    }
}
