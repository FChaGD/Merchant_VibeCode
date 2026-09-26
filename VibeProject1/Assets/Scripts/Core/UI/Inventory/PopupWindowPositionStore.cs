using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 팝업 창의 마지막 위치를 popupId별로 보관한다. 팝업 패널(plain C#)은 Hub 로드마다 새로 만들어지므로
    /// 영속 컴포넌트(HubUIWiring)가 이 저장소를 소유해 게임 실행 중 위치를 유지한다. 저장 시스템이 없어
    /// 게임을 재시작하면 기본 위치로 돌아간다(Docs/기획/39번 §3.8, 설계 40번 §5.1).
    /// </summary>
    public class PopupWindowPositionStore
    {
        private readonly Dictionary<string, Vector2> positionsById = new();

        public bool TryGet(string popupId, out Vector2 position) => positionsById.TryGetValue(popupId, out position);

        public void Set(string popupId, Vector2 position) => positionsById[popupId] = position;
    }
}
