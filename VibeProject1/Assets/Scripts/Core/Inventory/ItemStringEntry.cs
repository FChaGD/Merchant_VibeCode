using System;

namespace Game.Core
{
    /// <summary>
    /// 공용 스트링 테이블(Item.xlsx ItemStrings) 한 행(Docs/설계/50번 §5.1). 설명 열이 추가되면서 다른 도메인과
    /// 공유하는 SlugLocalizedStringEntry(Id, Ko)로는 담을 수 없어 아이템 전용으로 분리했다. 필드 이름(Id, Ko)은
    /// 기존 직렬화 값과 호환되도록 그대로 둔다.
    /// </summary>
    [Serializable]
    public struct ItemStringEntry
    {
        public string Id;
        public string Ko;
        public string DescKo;
    }
}
