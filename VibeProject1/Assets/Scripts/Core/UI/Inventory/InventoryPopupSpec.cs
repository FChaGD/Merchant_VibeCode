using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1종의 정의(Docs/설계/42번 §3). 에디터 인스톨러(화면 구성)와 런타임 배선(기능 동작)이 같은
    /// 정의를 읽어야 "인스톨러는 임시 보관을 만들었는데 런타임은 끔" 같은 불일치가 생기지 않는다. 회전 허용은
    /// 카탈로그가 전부 정사각형인지로 자동 판별하지 않는다 - 테이블 내용에 따라 조작 규칙이 바뀌면 예측하기 어렵다.
    /// </summary>
    public sealed class InventoryPopupSpec
    {
        public string PopupId { get; }
        public string Title { get; }
        public bool AllowsRotation { get; }
        // 임시 보관 영역. 켜면 닫기 차단과 "상행 시작" 비활성 조건도 함께 적용된다(설계 40번 §5.4~5.5).
        public bool HasStaging { get; }
        public Vector2 DefaultAnchorMin { get; }
        public Vector2 DefaultAnchorMax { get; }

        public InventoryPopupSpec(string popupId, string title, bool allowsRotation, bool hasStaging, Vector2 defaultAnchorMin, Vector2 defaultAnchorMax)
        {
            PopupId = popupId;
            Title = title;
            AllowsRotation = allowsRotation;
            HasStaging = hasStaging;
            DefaultAnchorMin = defaultAnchorMin;
            DefaultAnchorMax = defaultAnchorMax;
        }
    }

    public static class InventoryPopupSpecs
    {
        // 기획 39번 - RE4 아타셰 케이스 레퍼런스 전부 적용.
        public static readonly InventoryPopupSpec TradeGoods = new(
            InventoryPopupIds.TradeGoods, "상단 물류품", allowsRotation: true, hasStaging: true,
            new Vector2(0.02f, 0.16f), new Vector2(0.55f, 0.8f));

        // 기획 41번 - 장비는 전부 1×1이라 회전·임시 보관 없음. 두 팝업을 동시에 열어도 완전히 겹치지 않게
        // 상단 물류품보다 오른쪽·아래로 어긋나게, 더 작게 둔다(잠정 수치, 실전 확인 후 조정).
        public static readonly InventoryPopupSpec Equipment = new(
            InventoryPopupIds.Equipment, "전투 장비", allowsRotation: false, hasStaging: false,
            new Vector2(0.06f, 0.1f), new Vector2(0.42f, 0.72f));

        // 기획 43번 - 전투 장비와 같은 기능 구성. 창 제목은 버튼 라벨("소모품"/"개인 물품")이 아닌 정식 명칭(사용자 확정).
        // 네 팝업을 모두 열어도 완전히 가리지 않도록 계단식으로 어긋나게, 그리드 크기에 맞춰 작게 둔다(잠정 수치).
        public static readonly InventoryPopupSpec Consumable = new(
            InventoryPopupIds.Consumable, "전투 소모품", allowsRotation: false, hasStaging: false,
            new Vector2(0.1f, 0.06f), new Vector2(0.42f, 0.66f));

        public static readonly InventoryPopupSpec PersonalItem = new(
            InventoryPopupIds.PersonalItem, "상단주 개인 물품", allowsRotation: false, hasStaging: false,
            new Vector2(0.14f, 0.04f), new Vector2(0.36f, 0.56f));
    }
}
