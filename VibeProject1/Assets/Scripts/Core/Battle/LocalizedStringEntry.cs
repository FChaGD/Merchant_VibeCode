using System;

namespace Game.Core
{
    // Id→한국어 텍스트 조회용 공용 엔트리(Docs/설계/18번 §5.1) - 8개 enum(RoleGroup 제외, 화면에
    // 노출되지 않는 내부 분류값이라 String 시트가 없다)의 String 테이블이 전부 이 구조다. 사람이
    // 인스펙터에서 직접 편집하지 않고 임포터만 채우는 순수 임포트 산출물이라, RoleGroupTacticsCatalogAsset가
    // 축마다 별도 struct로 나눈 이유(제네릭 SO의 인스펙터 편집 불안정)가 여기엔 적용되지 않는다.
    [Serializable]
    public struct LocalizedStringEntry
    {
        public int Id;
        public string Ko;
    }
}
