using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 유닛 팔레트의 드래그 가능한 아이콘 하나 - 아군 직업 또는 적 타입 중 하나를
    /// 나타낸다. FormationUnitIconView와 같은 "드래그 이벤트를 델리게이트로 위임" 패턴이지만, 이
    /// 아이콘의 소비자는 BattleTestUnitPaletteView 하나뿐이라 SetHandlers 인자를 3개로 단순화했다
    /// (클릭 처리 자체가 필요 없음).
    /// </summary>
    public class BattleTestPaletteIconView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        // BindAlly/BindEnemy는 런타임이 아니라 에디터 인스톨러(BattleTestSceneInstaller)가 씬 저장
        // 시점에 호출한다(FormationUnitIconView.Bind와 다른 점 - 그쪽은 매 Play마다 런타임에 호출돼
        // 직렬화가 필요 없다). 자동 프로퍼티({ get; private set; })는 백킹 필드가 [SerializeField]가
        // 아니라 씬 저장/Play 진입 시 사라진다 - 실제로 이 문제 때문에 Play 모드에서 모든 아이콘이
        // 기본값(IsAlly=false, MercenaryClass/EnemyType=0번째 값)으로 리셋되는 버그가 있었다.
        [SerializeField] private bool isAlly;
        [SerializeField] private MercenaryClass mercenaryClass;
        [SerializeField] private EnemyType enemyType;

        public bool IsAlly => isAlly;
        public MercenaryClass MercenaryClass => mercenaryClass;
        public EnemyType EnemyType => enemyType;
        public Sprite Icon => iconImage != null ? iconImage.sprite : null;

        private Action<BattleTestPaletteIconView, PointerEventData> onBeginDrag;
        private Action<PointerEventData> onDrag;
        private Action<PointerEventData> onEndDrag;

        public void BindAlly(MercenaryClass unitClass, Sprite icon)
        {
            isAlly = true;
            mercenaryClass = unitClass;
            if (iconImage != null) iconImage.sprite = icon;
        }

        public void BindEnemy(EnemyType type, Sprite icon)
        {
            isAlly = false;
            enemyType = type;
            if (iconImage != null) iconImage.sprite = icon;
        }

        public void SetHandlers(
            Action<BattleTestPaletteIconView, PointerEventData> beginDragHandler,
            Action<PointerEventData> dragHandler,
            Action<PointerEventData> endDragHandler)
        {
            onBeginDrag = beginDragHandler;
            onDrag = dragHandler;
            onEndDrag = endDragHandler;
        }

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke(eventData);
    }
}
