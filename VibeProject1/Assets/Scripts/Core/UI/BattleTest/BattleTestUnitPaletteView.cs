using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 아군 3종(전사/궁수/방패병) + 적 3종(약탈자/괴수/적대자) 아이콘을 한 줄에 배치한다. 아이콘을
    /// 전장으로 드래그하면 ILiveUnitSpawner.SpawnAlly/SpawnEnemy를 호출한다 - 세팅 단계인지 전투
    /// 진행 중인지는 이 클래스가 몰라도 된다(ILiveUnitSpawner 구현체가 알아서 분기).
    /// FormationDragCoordinator(정비창)와 같은 "고스트를 화면 좌표로 드래그" 패턴이지만, 이 팔레트는
    /// 소비자가 하나뿐이라 별도 코디네이터 없이 이 클래스가 직접 드래그를 처리한다.
    /// </summary>
    public class BattleTestUnitPaletteView : MonoBehaviour
    {
        // BattleCharacterUnitView.BaseBodySize(실제 배치 유닛 스프라이트의 기준 월드 지름)와 동일한
        // 값 - 고스트 크기를 실제 유닛 크기와 동기화하기 위한 기준치다(유닛 타입별 HP 편차는 드래그
        // 미리보기 단계에선 무시). 두 값이 갈라지면 다시 어긋나므로 바뀌면 함께 맞출 것.
        private const float ReferenceUnitWorldDiameter = 0.6f;

        // 매번 Instantiate/Destroy하지 않고 하나만 만들어 켜고 끈다(CLAUDE.md 최적화 컨벤션 - UI
        // 오브젝트 get-or-create 재사용). 인스톨러가 Canvas 아래에 미리 만들어두고 처음엔 꺼둔다.
        [SerializeField] private Image dragGhost;
        [SerializeField] private BattleFieldWorldCameraView cameraView;
        [SerializeField] private BattleTestPaletteIconView[] allyIcons;
        [SerializeField] private BattleTestPaletteIconView[] enemyIcons;

        private ILiveUnitSpawner spawner;
        private BattleTestPaletteIconView draggingSource;

        public void Bind(ILiveUnitSpawner spawner)
        {
            this.spawner = spawner;

            foreach (var icon in allyIcons) icon.SetHandlers(HandleBeginDrag, HandleDrag, HandleEndDrag);
            foreach (var icon in enemyIcons) icon.SetHandlers(HandleBeginDrag, HandleDrag, HandleEndDrag);
        }

        private void HandleBeginDrag(BattleTestPaletteIconView source, PointerEventData eventData)
        {
            draggingSource = source;
            dragGhost.sprite = source.Icon;
            dragGhost.transform.position = eventData.position;
            UpdateGhostSize();
            dragGhost.gameObject.SetActive(true);
        }

        private void HandleDrag(PointerEventData eventData)
        {
            dragGhost.transform.position = eventData.position;
            // 드래그 도중 휠 줌이 바뀌어도 고스트가 계속 실제 배치 크기를 따라가도록 매 프레임 갱신.
            UpdateGhostSize();
        }

        // 실제 배치되는 유닛은 월드 오브젝트라 카메라를 줌아웃하면 화면상 작게 보이는데, 고스트는
        // 고정 크기 UI라 전투 시작 전 요구사항으로 지적된 "줌과 따로 노는" 문제가 있었다 - 매 프레임
        // 현재 줌 배율로 화면 픽셀 지름을 다시 계산해 맞춘다.
        private void UpdateGhostSize()
        {
            if (cameraView == null) return;

            var pixelsPerWorldUnit = Screen.height / (2f * cameraView.CurrentOrthographicSize);
            var screenDiameter = ReferenceUnitWorldDiameter * pixelsPerWorldUnit;
            // Screen Space Overlay 캔버스는 CanvasScaler가 sizeDelta(캔버스 설계 단위)를
            // scaleFactor배 해서 실제 화면 픽셀로 그린다 - transform.position은 화면 픽셀 그대로
            // 대응되지만 sizeDelta는 그렇지 않으므로 scaleFactor로 나눠 보정해야 한다.
            var scaleFactor = dragGhost.canvas != null ? dragGhost.canvas.scaleFactor : 1f;
            var size = screenDiameter / Mathf.Max(scaleFactor, 0.0001f);
            dragGhost.rectTransform.sizeDelta = new Vector2(size, size);
        }

        private void HandleEndDrag(PointerEventData eventData)
        {
            dragGhost.gameObject.SetActive(false);

            if (draggingSource != null && cameraView != null && spawner != null)
            {
                var worldPos = cameraView.ScreenToWorld(eventData.position);
                if (draggingSource.IsAlly) spawner.SpawnAlly(draggingSource.MercenaryClass, worldPos);
                else spawner.SpawnEnemy(draggingSource.EnemyType, worldPos);
            }

            draggingSource = null;
        }
    }
}
