using TMPro;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행정보 패널. 예상 소요시간/거리, 위험도/난이도, 현재 편성 요약, 보상/교역 정보를 표시한다.
    /// </summary>
    public class TripSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text durationDistanceText;
        [SerializeField] private TMP_Text dangerText;
        [SerializeField] private TMP_Text formationSummaryText;
        [SerializeField] private TMP_Text rewardText;

        public void SetValues(string durationDistance, string danger, string formationSummary, string reward)
        {
            if (durationDistanceText != null)
            {
                durationDistanceText.text = $"예상 소요시간 / 거리: {durationDistance}";
            }

            if (dangerText != null)
            {
                dangerText.text = $"위험도 / 난이도: {danger}";
            }

            if (formationSummaryText != null)
            {
                formationSummaryText.text = $"현재 편성 요약: {formationSummary}";
            }

            if (rewardText != null)
            {
                rewardText.text = $"보상 / 교역 정보: {reward}";
            }
        }
    }
}
