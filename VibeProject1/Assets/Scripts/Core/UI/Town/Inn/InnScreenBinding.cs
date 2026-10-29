using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 숙소 + 휴식 진행 화면 바인딩(설계 103번 §4.3·§4.4) - 숙소의 "휴식"이 진행 화면을 열므로 함께 만든다. 둘 다 시설 화면 채널.
    /// 휴식 진행 중 재화·날짜 HUD 입력 차단 규칙을 기여한다(Docs/Refactor/2026-10-26_씬전용.md E-2).
    /// 의존성: 휴식 서비스·골드 보유·골드 지출(필수 - 지출은 시작 판정이 결제와 같은 기준이어야 해서 개인 골드 판정으로 대신하지 않는다), 날짜·골드 아이템 정보·
    /// 휴식 이벤트 실행기(선택), 숙소 탭 인벤토리 3종(배선의 인벤토리 표 - 하나라도 없으면 화면 전체 미등록).
    /// 휴식 연출 속도는 서비스(Placeholder)를 바꿔도 남도록 배선의 직렬화 값을 받는다.
    /// </summary>
    internal sealed class InnScreenBinding : HubScreenBinding<InnPanel>, IHideRuleSource
    {
        private readonly RestPacingSettings restPacing;
        private InnRestProgressPanel progressPanel;

        public InnScreenBinding(RestPacingSettings restPacing) => this.restPacing = restPacing;

        protected override InnPanel Create(HubScreenContext context)
        {
            var rest = context.Optional<IInnRestService>();
            var gold = context.Optional<IGoldHoldingsReader>();
            var spender = context.Optional<IGoldSpender>();
            if (rest == null || gold == null || spender == null)
            {
                Debug.LogWarning($"숙소 화면에 필요한 {nameof(IInnRestService)}/{nameof(IGoldHoldingsReader)}/{nameof(IGoldSpender)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            // 탭 = 장비·소모품·개인 물품 인벤토리(기획 94 4-3). 저장소가 없는 탭이 있으면 화면 전체를 등록하지 않는다.
            var tabSpecs = InventoryPopupSpecs.InnTabs;
            var tabSources = new List<(IInventoryReader Reader, IInventoryArrangement Arrangement)>();
            foreach (var spec in tabSpecs)
            {
                if (!TryFindSource(context.InventorySources, spec, out var found))
                {
                    Debug.LogWarning($"숙소 '{spec.Title}' 탭 저장소가 없어 숙소 화면을 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                    return null;
                }
                tabSources.Add((found.Reader, found.Arrangement));
            }

            var sceneUIRoot = context.SceneUIRoot;
            if (!InnElements.TryBind(sceneUIRoot, tabSpecs, out var innElements) || !InnRestProgressElements.TryBind(sceneUIRoot, out var progressElements)) return null;

            progressPanel = new InnRestProgressPanel(progressElements, rest, context.Optional<IDateReader>(), gold, spender, context.Optional<IGoldItemInfo>(),
                context.Optional<IRestEventRunner>(), context.UIManager, context.Runner, restPacing);
            context.Panels.RegisterScreenPanel(progressPanel);
            var panel = new InnPanel(innElements, tabSources, rest, gold, spender, context.Tooltips, progressPanel, context.UIManager, context.Input);
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }

        public void CollectHideRules(SceneUIRoot sceneUIRoot, List<LayerHideRule> rules)
        {
            if (progressPanel == null) return;
            if (!sceneUIRoot.TryGetElement<CanvasGroup>(HubUIElementIds.PopupExemptLayer, out var hudLayer))
            {
                Debug.LogWarning("Hub UI에서 재화 HUD 레이어 CanvasGroup을 찾지 못해 휴식 진행 중 HUD 입력을 막지 않는다(Tools > Game > Build Hub Scene).");
                return;
            }
            rules.Add(new LayerHideRule(progressPanel.RestingCondition, new[] { hudLayer }, LayerHideMode.BlockInput));
        }

        public override void Dispose()
        {
            base.Dispose();
            progressPanel?.Dispose();
            progressPanel = null;
        }

        private static bool TryFindSource(IReadOnlyList<InventoryPopupSource> sources, InventoryPopupSpec spec, out InventoryPopupSource found)
        {
            foreach (var source in sources)
            {
                if (source.Spec.PopupId != spec.PopupId) continue;
                found = source;
                return true;
            }
            found = default;
            return false;
        }
    }
}
