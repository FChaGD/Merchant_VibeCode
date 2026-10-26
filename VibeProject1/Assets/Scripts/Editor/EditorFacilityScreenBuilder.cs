using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 마을 시설 화면의 공통 틀(Docs/Refactor/2026-10-26_공통.md D-3) - 화면 전체 루트, 좌상단 나가기 버튼, 머리글 달린 좌·우 패널.
    /// 무역품 구매·목록형 구매(용병·마구간)·숙소·상인조합 조립이 같은 틀을 각자 복사하고 있었다. 패널 윗변(PanelTop)은 우상단 재화 HUD와
    /// 겹치지 않게 제한한 값이다. 좌우 비율은 화면마다 달라 인자로 받는다.
    /// </summary>
    internal static class EditorFacilityScreenBuilder
    {
        public const float PanelBottom = 0.06f;
        public const float PanelTop = 0.86f;

        // 시설 화면 루트 오브젝트 이름 - 각 조립과 Hub 인스톨러의 옛 위치(ModalPopups) 이전 처리가 같은 이름을 써야 재사용이 맞물린다.
        public const string TradeGoodsMarketRoot = "TradeGoodsMarket";
        public const string MercenaryContactRoot = "MercenaryContact";
        public const string StableRoot = "Stable";
        public const string InnRoot = "Inn";
        public const string InnRestProgressRoot = "InnRestProgress";
        public const string MerchantGuildRoot = "MerchantGuild";

        public static readonly string[] AllRoots = { TradeGoodsMarketRoot, MercenaryContactRoot, StableRoot, InnRoot, InnRestProgressRoot, MerchantGuildRoot };

        /// <summary>화면 전체를 덮는 루트(get-or-create). 저장 상태 비활성은 호출부가 조립을 마친 뒤 정한다.</summary>
        public static RectTransform EnsureRoot(Transform screens, string name, string rootId)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(screens, name);
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, rootId);
            return rootRect;
        }

        public static GameObject EnsureExitButton(RectTransform root, string id)
            => EditorUIBuilder.EnsureLabeledButton(root, "ExitButton", "나가기", id, new Vector2(0.02f, 0.88f), new Vector2(0.14f, 0.95f), EditorUIColors.Button);

        /// <summary>패널(배경 + 위 10% 머리글). 세로는 PanelBottom~PanelTop 고정.</summary>
        public static RectTransform EnsurePanel(RectTransform root, string name, string title, float xMin, float xMax)
            => EditorUIBuilder.EnsureHeaderedPanel(root, name, title, new Vector2(xMin, PanelBottom), new Vector2(xMax, PanelTop), EditorUIColors.Panel, EditorUIColors.Header);
    }
}
