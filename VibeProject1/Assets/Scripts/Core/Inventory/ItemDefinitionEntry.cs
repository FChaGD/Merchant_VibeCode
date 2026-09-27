using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 아이템 카테고리 공통 데이터(Docs/설계/35번 §3). 엑셀은 공용/카테고리 테이블로 나뉘어 있지만 임포터가 Id로
    /// 조인해 카테고리별 자산에 이 구조체로 기록한다(설계 50번 §4.1) - 저장소들의 조회 코드를 바꾸지 않기 위함이다. Icon은 엑셀 셀의
    /// IconPath(문자열)를 임포터가 AssetDatabase로 미리 로드해 넣은 결과라, 런타임에는 경로 문자열이
    /// 아니라 이미 로드된 Sprite 참조만 남는다.
    /// </summary>
    [Serializable]
    public struct ItemDefinitionEntry
    {
        public string Id;
        public int FootprintWidth;
        public int FootprintHeight;
        public Sprite Icon;
        // 공용 데이터의 판매가(기획 49번 §3.2). 판매 목록 제공자만 읽는다 - 아이템 정의(IInventoryItemDefinition)에는 노출하지 않는다.
        public int Price;
    }
}
