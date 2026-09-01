using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 대열(아군 배치 기준) 범위를 전장에 사각형으로 표시하고, 모서리 드래그 또는 X/Y 숫자 입력으로
    /// BattleTestFieldLayout.ColumnCount/RowCount를 바꾼다(요구사항 #2/#3). 이미 스폰된 유닛의
    /// 도주거리 등은 스폰 시점에 값으로 박혀 재계산되지 않으므로, 이 박스는 "지금 이 순간의 설정
    /// 미리보기"이자 "이후 새로 스폰될 유닛부터 적용될 기준"이다(계획 확인 사항).
    /// </summary>
    public class BattleTestExtentGizmoView : MonoBehaviour
    {
        [SerializeField] private LineRenderer boxRenderer;
        [SerializeField] private BattleTestDragHandle[] cornerHandles;
        [SerializeField] private TMP_InputField columnCountInput;
        [SerializeField] private TMP_InputField rowCountInput;
        [SerializeField] private Button applyButton;
        [SerializeField] private BattleFieldWorldCameraView cameraView;
        [SerializeField] private BattleTestSimulationRule simulationRule;

        private BattleTestFieldLayout FieldLayout => simulationRule.FieldLayout;

        private void Start()
        {
            foreach (var handle in cornerHandles) handle.SetHandler(HandleCornerDrag);
            applyButton.onClick.AddListener(HandleApplyClicked);
            SyncInputsFromLayout();
        }

        private void Update()
        {
            DrawBox();
            RepositionHandles();
        }

        private void DrawBox()
        {
            var min = FieldLayout.ExtentMin;
            var max = FieldLayout.ExtentMax;
            boxRenderer.positionCount = 5;
            boxRenderer.SetPosition(0, new Vector3(min.x, min.y, 0f));
            boxRenderer.SetPosition(1, new Vector3(max.x, min.y, 0f));
            boxRenderer.SetPosition(2, new Vector3(max.x, max.y, 0f));
            boxRenderer.SetPosition(3, new Vector3(min.x, max.y, 0f));
            boxRenderer.SetPosition(4, new Vector3(min.x, min.y, 0f));
        }

        // 4개 핸들 전부 "어느 모서리인지" 구분하지 않는다 - SetExtentFromCorner가 절댓값만 쓰므로
        // 아무 모서리나 드래그해도 대칭으로 반영된다(대열이 항상 원점 중심이라는 전제와 일치).
        private void RepositionHandles()
        {
            if (cornerHandles.Length != 4 || Camera.main == null) return;

            var min = FieldLayout.ExtentMin;
            var max = FieldLayout.ExtentMax;
            var corners = new[]
            {
                new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f),
                new Vector3(max.x, max.y, 0f), new Vector3(min.x, max.y, 0f),
            };
            for (var i = 0; i < 4; i++)
            {
                cornerHandles[i].transform.position = Camera.main.WorldToScreenPoint(corners[i]);
            }
        }

        private void HandleCornerDrag(PointerEventData eventData)
        {
            if (cameraView == null) return;

            var worldPos = cameraView.ScreenToWorld(eventData.position);
            FieldLayout.SetExtentFromCorner(worldPos);
            SyncInputsFromLayout();
        }

        private void HandleApplyClicked()
        {
            if (int.TryParse(columnCountInput.text, out var columns)) FieldLayout.ColumnCount = Mathf.Max(1, columns);
            if (int.TryParse(rowCountInput.text, out var rows)) FieldLayout.RowCount = Mathf.Max(1, rows);
            SyncInputsFromLayout();
        }

        private void SyncInputsFromLayout()
        {
            columnCountInput.SetTextWithoutNotify(FieldLayout.ColumnCount.ToString());
            rowCountInput.SetTextWithoutNotify(FieldLayout.RowCount.ToString());
        }
    }
}
