using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 개인 골드 → 골드 상자 변환 모달(Docs/설계/83번 §6.2, 기획 82번 B2~B5). 모달 팝업 채널에 등록된다 - 상행 준비 UI가 열린 상태에서
    /// HUD를 눌러 열면, 닫을 때 채널의 기존 복귀 동작으로 상행 준비 UI가 돌아온다. plain C#이며 HubUIWiring이 Hub 로드마다 새로 만든다 -
    /// 골드 보유(Bootstrap 상주) 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// </summary>
    public sealed class GoldConversionPanel : IUIPanel, IDisposable
    {
        private readonly GoldConversionElements elements;
        private readonly IGoldHoldingsReader holdings;
        private readonly IGoldBoxConverter converter;
        private int count;
        private bool isOpen;

        public string PanelId => UIPanelIds.GoldConversion;

        public GoldConversionPanel(GoldConversionElements elements, IGoldHoldingsReader holdings, IGoldBoxConverter converter, IUIManager uiManager)
        {
            this.elements = elements;
            this.holdings = holdings;
            this.converter = converter;

            elements.IncreaseButton.onClick.RemoveAllListeners();
            elements.IncreaseButton.onClick.AddListener(() => SetCount(count + 1));
            elements.DecreaseButton.onClick.RemoveAllListeners();
            elements.DecreaseButton.onClick.AddListener(() => SetCount(count - 1));
            elements.ExcessAllButton.onClick.RemoveAllListeners();
            elements.ExcessAllButton.onClick.AddListener(() => SetCount(converter.ExcessBoxes));
            elements.ConvertButton.onClick.RemoveAllListeners();
            elements.ConvertButton.onClick.AddListener(Convert);
            // 닫기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 모달 채널의 복귀가 함께 처리된다.
            elements.CloseButton.onClick.RemoveAllListeners();
            elements.CloseButton.onClick.AddListener(() => uiManager.Close(PanelId));

            holdings.Changed += HandleHoldingsChanged;
            elements.Root.SetActive(false);
        }

        // 열 때 스테퍼 초기값은 "초과분 전부" 개수(사용자 결정 2026-10-10) - 열자마자 [변환] 한 번으로 초과를 해소할 수 있게.
        public void Open()
        {
            isOpen = true;
            elements.Root.SetActive(true);
            SetCount(converter.ExcessBoxes);
        }

        // Close()는 표시/숨김만 한다. 닫기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            elements.Root.SetActive(false);
        }

        public void Dispose() => holdings.Changed -= HandleHoldingsChanged;

        private void Convert()
        {
            converter.Convert(count);
            // 변환 후에도 열린 채로 두고, 남은 초과분 기준으로 다시 맞춘다(설계 83번 §6.2).
            SetCount(converter.ExcessBoxes);
        }

        private void HandleHoldingsChanged()
        {
            if (isOpen) SetCount(count);
        }

        private void SetCount(int value)
        {
            var max = converter.MaxConvertibleBoxes;
            count = Mathf.Clamp(value, 0, max);
            Render(max);
        }

        private void Render(int max)
        {
            var value = converter.GoldBoxValue;
            var personalAfter = holdings.PersonalGold - count * value;
            var excessAfter = Mathf.Max(0, personalAfter - holdings.PersonalLimit);

            elements.InfoText.text =
                $"개인 골드 {holdings.PersonalGold:N0}\n" +
                $"개인 소유 가능량 {holdings.PersonalLimit:N0}\n" +
                $"빈 칸 {holdings.FreeCells:N0}\n\n" +
                $"변환 후 개인 골드 {personalAfter:N0}\n" +
                $"출발 시 버려질 골드 {excessAfter:N0}";
            elements.CountText.text = $"골드 상자 {count}개 ({count * value:N0})";

            elements.IncreaseButton.interactable = count < max;
            elements.DecreaseButton.interactable = count > 0;
            elements.ExcessAllButton.interactable = converter.ExcessBoxes > 0;
            elements.ConvertButton.interactable = count > 0;
        }
    }
}
