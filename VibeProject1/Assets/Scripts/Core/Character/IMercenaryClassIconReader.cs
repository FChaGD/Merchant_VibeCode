using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 직업 아이콘 조회(Docs/설계/54번 §4.2). 아이콘이 테이블에 없고 Placeholder 로스터의 인스펙터 필드에만 있어 그 클래스가
    /// 구현한다 - 아이콘 데이터가 테이블로 옮겨 가면 구현체만 바꾼다.
    /// </summary>
    public interface IMercenaryClassIconReader
    {
        Sprite GetClassIcon(string mercenaryClass);
    }
}
