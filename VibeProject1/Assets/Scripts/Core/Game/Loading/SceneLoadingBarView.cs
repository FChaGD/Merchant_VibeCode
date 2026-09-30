using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 검은 커튼 위 로딩바("로딩 중" 문구·게이지·현재 작업 이름·백분율, Docs/설계/67번 §4.7). 진행 상태의 변경 이벤트로만 갱신한다
    /// (Update 폴링 없음). 커튼과 함께 Bootstrap(영속)에 있고, 표시·숨김은 연출 컨트롤러가 정한다.
    /// </summary>
    public class SceneLoadingBarView : MonoBehaviour
    {
        // 게이지 채움 - 가로 앵커 끝을 진행률로 옮긴다(스프라이트 없이도 동작하도록 Filled 이미지 대신).
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text stepLabel;
        [SerializeField] private TMP_Text percentLabel;

        private ISceneLoadingProgressReader progress;

        public void Bind(ISceneLoadingProgressReader reader)
        {
            if (progress != null) progress.Changed -= Refresh;
            progress = reader;
            if (progress != null) progress.Changed += Refresh;
            Refresh();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (progress == null) return;

            var total = progress.TotalCount;
            var ratio = total == 0 ? 1f : (float)progress.CompletedCount / total;
            if (fill != null) fill.anchorMax = new Vector2(ratio, fill.anchorMax.y);
            if (stepLabel != null) stepLabel.text = progress.CurrentLabel;
            if (percentLabel != null) percentLabel.text = $"{Mathf.FloorToInt(ratio * 100f)}%";
        }
    }
}
