using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 이벤트 중 이동 뷰 가림 적용(설계 89 §13.2) - plain C#, FieldUIWiring이 Field 로드마다 만든다. 이동 뷰 루트의 CanvasGroup 하나로 가린다: 구간 도착
    /// 알림·전투 경고는 자기 CanvasGroup으로 페이드·점멸하므로 요소마다 그룹을 붙이면 가림과 서로 덮어쓴다. 배경은 자기 CanvasGroup의 ignoreParentGroups로
    /// 남는다(인스톨러). 오브젝트를 끄지 않는다 - 게이지·알림의 구독·코루틴이 끊기지 않게. 만들 때 현재 상태를 바로 반영한다(재로드 시 이미 가림일 수 있다).
    /// </summary>
    public sealed class FieldMovementViewHider : IDisposable
    {
        private readonly CanvasGroup group;
        private readonly IFieldUIHideReader reader;

        public FieldMovementViewHider(CanvasGroup group, IFieldUIHideReader reader)
        {
            this.group = group;
            this.reader = reader;
            reader.Changed += Apply;
            Apply();
        }

        private void Apply()
        {
            if (group == null) return;   // Field 언로드 뒤(Unity 파괴 판정) - 다음 로드에서 새 객체가 맡는다
            var hidden = reader.IsHidden;
            group.alpha = hidden ? 0f : 1f;
            group.interactable = !hidden;
            group.blocksRaycasts = !hidden;
        }

        public void Dispose() => reader.Changed -= Apply;
    }
}
