using System;
using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 휴식 진행 화면(기획 94 4-7~4-13, Docs/설계/103번 §4.4). 숙소와 같은 시설 화면 채널의 별도 패널이라, 열면 채널이 숙소를 숨기고
    /// 닫으면 숙소로 돌아간다 - 숙소 패널이 설정·진행·결과를 내부에서 바꿔 끼우지 않는다(SRP). 진행이 끝나면 같은 화면이 결과 상태로 바뀐다.
    /// plain C# 패널은 프레임 갱신이 없어 Bootstrap 상주 MonoBehaviour(HubUIWiring)를 실행 주체로 받아 코루틴을 돌리고, 배속과 무관하게
    /// 실제 시간(unscaledDeltaTime)으로 진행한다.
    /// 날짜·골드는 표시용 예상값이다 - 실제 변경은 휴식 서비스 구현체(system 브랜치) 몫. 진행 중에는 재화·날짜 HUD 레이어 입력을 막는다 -
    /// 골드 변환으로 개인 골드가 바뀌면 "진행 중 부족 없음" 전제가 깨지기 때문이다(사용자 결정 2026-10-20, 설계 103번 §3). 시설 화면 채널 분리(Docs/Refactor/2026-10-26_공통.md B-1) 뒤로는 모달이 진행 화면을 닫지 않지만 이 전제 때문에 차단은 유지한다(B-5).
    /// </summary>
    public sealed class InnRestProgressPanel : IUIPanel, IDisposable
    {
        private const string MissingText = PlaceholderText.Missing;

        private readonly InnRestProgressElements elements;
        private readonly IInnRestService rest;
        private readonly IDateReader date;         // 선택적 - 없으면 예상 날짜 "값 없음"
        private readonly IGoldHoldingsReader gold;
        private readonly IPanelNavigator uiManager;
        private readonly MonoBehaviour runner;
        private readonly RestPacingSettings pacing;
        private readonly CanvasGroup hudLayer;     // 선택적 - 없으면(인스톨러 미실행) 차단 없이 동작

        private RestProgressRun run;
        private GameDate? startDate;
        private int startGold;
        private int costPerDay;
        private bool ended;
        private Coroutine ticking;
        private bool hudBlocked;
        private bool hudWasInteractable;
        private bool hudWasBlockingRaycasts;

        public string PanelId => UIPanelIds.InnRestProgress;

        public InnRestProgressPanel(InnRestProgressElements elements, IInnRestService rest, IDateReader date, IGoldHoldingsReader gold, IPanelNavigator uiManager, MonoBehaviour runner, RestPacingSettings pacing, CanvasGroup hudLayer)
        {
            this.elements = elements;
            this.rest = rest;
            this.date = date;
            this.gold = gold;
            this.uiManager = uiManager;
            this.runner = runner;
            this.pacing = pacing ?? new RestPacingSettings();
            this.hudLayer = hudLayer;
            if (hudLayer == null) Debug.LogWarning("Hub UI에서 재화 HUD 레이어 CanvasGroup을 찾지 못해 휴식 진행 중 HUD 입력을 막지 않는다(Tools > Game > Build Hub Scene).");

            elements.StopButton.onClick.RemoveAllListeners();
            elements.StopButton.onClick.AddListener(StopRest);
            // 확인은 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 채널이 숙소로 되돌린다.
            elements.ConfirmButton.onClick.RemoveAllListeners();
            elements.ConfirmButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.Root.SetActive(false);
        }

        /// <summary>숙소의 "휴식"에서 부른다. 시작 시점 날짜·개인 골드·1일 비용을 고정해 예상값을 계산한다.</summary>
        public void Begin(int days)
        {
            startDate = date != null ? date.Today : (GameDate?)null;
            startGold = gold?.PersonalGold ?? 0;
            costPerDay = rest.CostPerDay;
            run = new RestProgressRun(days, RestPacing.SecondsPerDay(days, pacing));
            ended = false;
            rest.BeginRest(days);
            uiManager.Open(PanelId);
        }

        public void Open()
        {
            if (run == null) return;

            elements.Root.SetActive(true);
            elements.ResultLabel.text = string.Empty;
            BlockHud();
            if (ended)
            {
                ShowResult();
                return;
            }

            elements.StopButton.gameObject.SetActive(true);
            elements.ConfirmButton.gameObject.SetActive(false);
            Render();
            StopTicking();
            ticking = runner.StartCoroutine(Tick());
        }

        // Close()는 표시/숨김만 한다. 확인 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        // 진행 중에 다른 경로로 닫히면(씬 전환 등) 지난 일수까지만 휴식한 것으로 끝낸다.
        public void Close()
        {
            StopTicking();
            EndRun(stopped: true);
            RestoreHud();
            elements.Root.SetActive(false);
        }

        public void Dispose()
        {
            StopTicking();
            EndRun(stopped: true);
            RestoreHud();
        }

        private IEnumerator Tick()
        {
            while (!run.IsFinished && !run.IsStopped)
            {
                yield return null;
                var newDays = run.Advance(Time.unscaledDeltaTime);
                for (var i = 0; i < newDays; i++) rest.AdvanceDay();
                // 게이지는 연속으로 차오르지만 문구는 하루가 지날 때만 바뀐다 - 매 프레임 문자열을 새로 만들지 않는다.
                RenderGauge();
                if (newDays > 0) RenderTexts();
            }

            ticking = null;
            if (run.IsFinished) Finish();
        }

        private void StopRest()
        {
            if (run == null || ended) return;
            run.Stop();
            Finish();
        }

        private void Finish()
        {
            StopTicking();
            EndRun(stopped: run.IsStopped);
            ShowResult();
        }

        private void EndRun(bool stopped)
        {
            if (run == null || ended) return;
            if (stopped) run.Stop();
            ended = true;
            rest.EndRest(run.ElapsedDays);
        }

        // 결과는 별도 창이 아니라 같은 화면에서 보인다(기획 94 4-13) - 게이지 정지, "휴식 중단" 자리에 "확인".
        private void ShowResult()
        {
            Render();
            var verb = run.IsFinished ? "완료" : "중단";
            elements.ResultLabel.text = $"{run.ElapsedDays}일 휴식 {verb}, 비용 {run.ElapsedDays * costPerDay:N0}";
            elements.StopButton.gameObject.SetActive(false);
            elements.ConfirmButton.gameObject.SetActive(true);
        }

        private void Render()
        {
            RenderGauge();
            RenderTexts();
        }

        private void RenderGauge()
        {
            var fill = elements.GaugeFill;
            if (fill == null) return; // Hub 언로드 뒤
            fill.anchorMax = new Vector2(run.Progress, fill.anchorMax.y);
        }

        private void RenderTexts()
        {
            if (elements.Root == null) return; // Hub 언로드 뒤

            var days = run.ElapsedDays;
            elements.DaysLabel.text = $"{days} / 총 {run.TotalDays}일";
            elements.DateLabel.text = "예상 날짜 " + (startDate.HasValue ? new GameDate(startDate.Value.TotalDays + days).ToDisplayString() : MissingText);
            elements.CostLabel.text = $"누적 비용 {days * costPerDay:N0}";
            elements.GoldLabel.text = $"예상 남은 개인 골드 {startGold - days * costPerDay:N0}";
        }

        private void StopTicking()
        {
            if (ticking == null) return;
            if (runner != null) runner.StopCoroutine(ticking);
            ticking = null;
        }

        private void BlockHud()
        {
            if (hudLayer == null || hudBlocked) return;
            hudWasInteractable = hudLayer.interactable;
            hudWasBlockingRaycasts = hudLayer.blocksRaycasts;
            hudLayer.interactable = false;
            hudLayer.blocksRaycasts = false;
            hudBlocked = true;
        }

        private void RestoreHud()
        {
            if (!hudBlocked) return;
            hudBlocked = false;
            if (hudLayer == null) return; // Hub 언로드로 이미 파괴됨
            hudLayer.interactable = hudWasInteractable;
            hudLayer.blocksRaycasts = hudWasBlockingRaycasts;
        }
    }
}
