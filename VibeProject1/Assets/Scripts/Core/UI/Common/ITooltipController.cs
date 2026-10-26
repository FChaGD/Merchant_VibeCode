using System;
using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 호버·길게 누름 툴팁의 등록 창구(UIManager 하위 컴포넌트, 사용자 결정 2026-10-20). 툴팁을 쓰는 곳이 각자 표시 상태를
    /// 들고 있지 않고 한 소유자에 모은다(Docs/설계/100번 §3·§4.1).
    /// </summary>
    public interface ITooltipController
    {
        /// <summary>중계 2개·툴팁 루트·문구 칸을 묶어 등록한다. 루트는 숨긴 채 시작한다.</summary>
        ITooltipHandle Register(PointerHoverRelay hover, PointerPressRelay press, GameObject tooltipRoot, TMP_Text text);
    }

    public interface ITooltipHandle : IDisposable
    {
        /// <summary>빈 문자열(또는 null)이면 표시하지 않는다 - 떠 있으면 즉시 닫힌다.</summary>
        void SetText(string text);
    }
}
