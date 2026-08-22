using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 드래그/배치 동작을 테스트하기 위한 임시 유닛 데이터.
    /// 실제 캐릭터/마차/시설 데이터 모델이 생기면 대체된다.
    /// </summary>
    public class PlaceholderFormationUnit : IFormationUnit
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public FormationUnitKind Kind { get; }

        public PlaceholderFormationUnit(string id, string displayName, Sprite icon, FormationUnitKind kind)
        {
            Id = id;
            DisplayName = displayName;
            Icon = icon;
            Kind = kind;
        }
    }
}
