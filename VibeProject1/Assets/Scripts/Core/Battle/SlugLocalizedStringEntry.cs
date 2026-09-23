using System;

namespace Game.Core
{
    // LocalizedStringEntry와 같은 역할(Id→한국어 텍스트)이지만 Id가 int가 아니라 문자열 슬러그인
    // 테이블용이다(Docs/설계/36번 §4.3 - enum 정수 Id를 문자열로 전환하며 필요해짐). 아이템
    // 시스템(33/35번)도 같은 구조를 재사용할 수 있어 이름을 특정 도메인 전용으로 짓지 않았다.
    [Serializable]
    public struct SlugLocalizedStringEntry
    {
        public string Id;
        public string Ko;
    }
}
