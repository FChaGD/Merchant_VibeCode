using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UI 레이어 숨김 규칙 게이트(Docs/Refactor/2026-10-26_공통.md B-4, 옛 PopupLayerGate). 씬 배선이 "조건 → 숨길 레이어" 규칙표를 등록하고,
    /// 조건이 바뀔 때마다 규칙 중 하나라도 참인 레이어를 숨긴다. 숨기지 않고 입력만 막는 규칙도 같은 표에 둔다(Docs/Refactor/2026-10-26_씬전용.md E-2). Hub는 모달·시설 화면 열림으로 Depth/Persistent 레이어를, Field는 이벤트 가림으로
    /// 이동 뷰를 숨긴다 - 씬마다 다른 장치를 쓰던 것을 하나로 합쳤다.
    /// SetActive가 아니라 CanvasGroup을 쓰는 이유: depth 축이 같은 하위 오브젝트들을 SetActive로 켜고 끄므로 두 축이 서로 다른 속성을 소유해야
    /// 하고, 오브젝트를 끄면 게이지·알림의 구독·코루틴이 끊긴다(설계 38번 §3, 설계 89 §13.2).
    /// 순수 C# 객체가 아니라 UIManager 산하 컴포넌트인 이유: 영속 객체의 신호에 씬이 다시 로드될 때마다 새 구독이 쌓이지 않도록 같은 인스턴스가
    /// 이전 구독을 해제하고 다시 구독해야 한다. 어떤 레이어를 숨길지는 씬 배선이 정한다 - 씬별 요소 Id를 이 클래스가 알지 않는다.
    /// </summary>
    public class LayerHideGate : MonoBehaviour, ILayerHideGate
    {
        private readonly List<LayerHideRule> rules = new();
        private readonly List<CanvasGroup> allLayers = new();
        private readonly List<(bool active, IReadOnlyList<CanvasGroup> layers)> hideSnapshot = new();
        private readonly List<(bool active, IReadOnlyList<CanvasGroup> layers)> blockSnapshot = new();

        public void Register(IReadOnlyList<LayerHideRule> newRules)
        {
            Unsubscribe();
            rules.Clear();
            allLayers.Clear();
            foreach (var rule in newRules)
            {
                rules.Add(rule);
                rule.Condition.Changed += Apply;
                foreach (var layer in rule.Layers)
                {
                    if (!allLayers.Contains(layer)) allLayers.Add(layer);
                }
            }
            Apply();
        }

        private void Apply()
        {
            hideSnapshot.Clear();
            blockSnapshot.Clear();
            foreach (var rule in rules)
            {
                var target = rule.Mode == LayerHideMode.Hide ? hideSnapshot : blockSnapshot;
                target.Add((rule.Condition.IsActive, rule.Layers));
            }
            var hidden = LayerHideResolver.HiddenLayers(hideSnapshot);
            var blocked = LayerHideResolver.HiddenLayers(blockSnapshot);

            foreach (var layer in allLayers)
            {
                // 씬 전환 직후 이전 씬의 레이어는 이미 파괴됐을 수 있다(채널 Reset 신호가 새 등록보다 먼저 온다).
                if (layer == null) continue;

                // 숨김이 우선 - 숨김이 풀려도 입력 차단 규칙이 참이면 보이되 누를 수 없다.
                var hide = hidden.Contains(layer);
                var block = hide || blocked.Contains(layer);
                layer.alpha = hide ? 0f : 1f;
                layer.blocksRaycasts = !block;
                layer.interactable = !block;
            }
        }

        private void Unsubscribe()
        {
            foreach (var rule in rules) rule.Condition.Changed -= Apply;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
