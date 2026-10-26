using System.Collections.Generic;

namespace Game.Core
{
    public interface ILayerHideGate
    {
        /// <summary>
        /// "조건 → 숨길 레이어" 규칙표를 등록한다(Docs/Refactor/2026-10-26_공통.md B-4). 씬이 다시 로드될 때마다 새 규칙으로 다시 호출된다 -
        /// 이전 규칙의 구독은 해제되고 대체된다. 등록 즉시 현재 조건 상태를 반영한다.
        /// </summary>
        void Register(IReadOnlyList<LayerHideRule> rules);
    }
}
