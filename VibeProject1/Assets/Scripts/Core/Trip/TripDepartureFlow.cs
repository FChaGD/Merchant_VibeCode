using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 출발 절차(Docs/Refactor/2026-10-26_씬전용.md D-1, SS-3) - 경고 수집 → 순차 확인 → 임시 보관 물류품·초과 골드 버림 → 구간 계획 확정 → 씬 전환 요청.
    /// 상행 준비 패널이 화면과 함께 갖고 있던 흐름을 떼어 냈다. 경고는 독립 판정·순차 표시하고(기획 82번 E4·E5, 설계 83번 §6.3), 모두 확인해야
    /// 버리고 출발하며 어느 단계든 취소하면 아무것도 바꾸지 않는다. 연결 조건(버튼 비활성)과는 별개다(기획 78 §4-35).
    /// 확인 창이 없으면(인스톨러 미실행) 경고 없이 출발한다. 구간 계획을 찾지 못해도 예전처럼 여정 없이 출발한다.
    /// 임시 보관은 예전엔 버튼을 이유 없이 비활성화했다 - 마지막 마차가 사라지면 다시 실을 곳이 없어 출발이 막혀 경고+버림으로 바꿨다(2026-10-15).
    /// </summary>
    public sealed class TripDepartureFlow
    {
        private const string ConfirmText = "출발";
        private const string CancelText = "취소";

        private readonly IConfirmPrompt prompt;
        private readonly IReadOnlyList<IInventoryStagingDiscarder> staging;
        private readonly IGoldHoldingsReader gold;
        private readonly IGoldDepartureSettlement goldDeparture;
        private readonly ITripDeparture departure;
        private readonly ITripCurrentLocationReader location;
        private readonly ITripDestinationReader destination;
        private readonly Action requestTransition;
        private readonly IQuestCargoWarnings questWarnings; // 선택적 - 임시 보관에 의뢰 화물이 있으면 경고에 퀘스트 이름을 더한다(설계 107번 §8.4)

        public TripDepartureFlow(IConfirmPrompt prompt, IReadOnlyList<IInventoryStagingDiscarder> staging, IGoldHoldingsReader gold, IGoldDepartureSettlement goldDeparture, ITripDeparture departure, ITripCurrentLocationReader location, ITripDestinationReader destination, Action requestTransition,
            IQuestCargoWarnings questWarnings = null)
        {
            this.questWarnings = questWarnings;
            this.prompt = prompt;
            this.staging = staging ?? Array.Empty<IInventoryStagingDiscarder>();
            this.gold = gold;
            this.goldDeparture = goldDeparture;
            this.departure = departure;
            this.location = location;
            this.destination = destination;
            this.requestTransition = requestTransition;
        }

        public void Start(bool hasPlacedUnit)
        {
            var warnings = TripDepartureWarnings.Collect(hasPlacedUnit, CountStagedItems(), gold?.ExcessGold ?? 0, gold?.PersonalLimit ?? 0, StagedQuestNames());
            ConfirmThenDepart(warnings, 0);
        }

        private IReadOnlyList<string> StagedQuestNames()
        {
            if (questWarnings == null) return null;
            var ids = new List<string>();
            foreach (var discarder in staging) ids.AddRange(InventoryDiscardWarning.ItemIds(discarder.StagedItems));
            return questWarnings.Describe(ids);
        }

        private void ConfirmThenDepart(IReadOnlyList<string> warnings, int index)
        {
            if (index >= warnings.Count || prompt == null)
            {
                Depart();
                return;
            }

            prompt.Show(warnings[index], ConfirmText, CancelText, () => ConfirmThenDepart(warnings, index + 1), null);
        }

        private void Depart()
        {
            foreach (var discarder in staging) discarder.DiscardStaged();
            goldDeparture?.DiscardExcess();

            if (departure != null && location != null && destination?.DestinationCityId is { } destinationCityId
                && !departure.TryDepart(location.CurrentCityId, destinationCityId))
            {
                Debug.LogWarning($"{nameof(TripDepartureFlow)}: 현재 위치({location.CurrentCityId}) → 도착지({destinationCityId}) 경로를 찾지 못해 여정 없이 출발한다.");
            }
            requestTransition?.Invoke();
        }

        private int CountStagedItems()
        {
            var count = 0;
            foreach (var discarder in staging) count += discarder.StagedItems.Count;
            return count;
        }
    }
}
