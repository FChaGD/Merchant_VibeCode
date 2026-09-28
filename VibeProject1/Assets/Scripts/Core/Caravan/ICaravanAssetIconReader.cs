using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 종류 아이콘 조회(Docs/설계/56번 §4). 아이콘이 테이블에 없고 Placeholder 로스터의 인스펙터 필드에만 있어 그 클래스가
    /// 구현한다(IMercenaryClassIconReader와 같은 이유).
    /// </summary>
    public interface ICaravanAssetIconReader
    {
        Sprite GetKindIcon(FormationUnitKind kind);
    }
}
