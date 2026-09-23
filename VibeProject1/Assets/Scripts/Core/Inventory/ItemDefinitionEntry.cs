using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 아이템 카테고리 4종(교역품/장비/소모품/개인물품) 공통 Data 시트 컬럼(Docs/설계/35번 §3) -
    /// 카테고리별 전용 필드는 아직 없어 이 구조체 하나를 4개 카테고리가 공유한다. Icon은 엑셀 셀의
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
    }
}
