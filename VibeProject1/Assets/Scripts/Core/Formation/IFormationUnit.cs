using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배치 UI가 다루는 유닛(캐릭터/마차/시설)의 공통 계약. 실제 데이터 모델은 각 시스템이 구현한다.
    /// </summary>
    public interface IFormationUnit
    {
        string Id { get; }
        string DisplayName { get; }
        Sprite Icon { get; }
        FormationUnitKind Kind { get; }
    }
}
