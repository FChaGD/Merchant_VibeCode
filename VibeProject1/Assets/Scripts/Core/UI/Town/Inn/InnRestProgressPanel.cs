using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 휴식 진행 화면(기획 94 4-7~4-13, 108 §3.4, Docs/설계/103번 §4.4·109번 §6.3). 숙소와 같은 시설 화면 채널의 별도 패널이라, 열면 채널이 숙소를 숨기고
    /// 닫으면 숙소로 돌아간다 - 숙소 패널이 설정·진행·결과를 내부에서 바꿔 끼우지 않는다(SRP). 진행이 끝나면 같은 화면이 결과 상태로 바뀐다.
    /// 하루 순서(대기 → 이벤트 → 결제)는 흐름 객체(InnRestDayFlow)가 갖고, 이 패널은 대기 시간 측정(RestProgressRun)과 표시만 한다 - 게이지는 대기 단계에서만
    /// 차오르고 이벤트·하루 처리 중엔 멈춘다.
    /// plain C# 패널은 프레임 갱신이 없어 Bootstrap 상주 MonoBehaviour(HubUIWiring)를 실행 주체로 받아 코루틴을 돌리고, 배속과 무관하게
    /// 실제 시간(unscaledDeltaTime)으로 진행한다. 문구는 흐름·골드 변경 때만 다시 만든다 - 매 프레임은 게이지만.
    /// 진행 화면이 열린 동안에는 재화·날짜 HUD 레이어 입력을 막는다 - 골드 변환으로 잔액이 바뀌면 "시작 시 총 비용 확인" 전제가 깨지기 때문이다(사용자 결정
    /// 2026-10-20, 설계 103번 §3). 차단은 이 패널이 레이어를 직접 만지지 않고 RestingCondition을 내면 Hub 배선이 숨김 게이트에 "입력 차단" 규칙으로 등록한다
    /// (Docs/Refactor/2026-10-26_씬전용.md E-2, SS-5 - 레이어 CanvasGroup 조작 경로를 게이트 하나로).
    /// </summary>
    public sealed class InnRestProgressPanel : IUIPanel, IInnRestStarter, IDisposable
    {
        private const string MissingText = PlaceholderText.Missing;
        // 이벤트 영역에 보이는 최근 기록 줄 수 - 휴식 최대 30일·하루 최대 2건이라 스크롤 없이 최근 것만 보인다(설계 109 §6.3).
        private const int MaxLogLines = 5;

        private readonly InnRestProgressElements elements;
        private readonly IInnRestService rest;
        private readonly IDateReader date;           // 선택적 - 없으면 날짜 "값 없음"
        private readonly IGoldHoldingsReader gold;
        private readonly IGoldSpender spender;       // 필수(숙소 화면 배선이 없으면 화면을 등록하지 않는다) - 결제와 같은 순서로 상자 인출을 예상한다
        private readonly IGoldItemInfo goldItems;    // 선택적 - 없으면 상자 가치 0
        private readonly IRestEventRunner events;    // 선택적 - 없으면 이벤트 없는 휴식
        private readonly IPanelNavigator uiManager;
        private readonly MonoBehaviour runner;
        private readonly RestPacingSettings pacing;
        private readonly StringBuilder log = new();

        private RestProgressRun run;
        private InnRestDayFlow flow;
        private int costPerDay;
        private bool isOpen;
        private Coroutine ticking;
        private readonly UIHideToggle resting = new();

        public string PanelId => UIPanelIds.InnRestProgress;

        /// <summary>진행 화면이 열려 있는 동안 참 - Hub 배선이 재화 HUD 입력 차단 규칙의 조건으로 쓴다.</summary>
        public IUIHideCondition RestingCondition => resting;

        public InnRestProgressPanel(InnRestProgressElements elements, IInnRestService rest, IDateReader date, IGoldHoldingsReader gold, IGoldSpender spender,
            IGoldItemInfo goldItems, IRestEventRunner events, IPanelNavigator uiManager, MonoBehaviour runner, RestPacingSettings pacing)
        {
            this.elements = elements;
            this.rest = rest;
            this.date = date;
            this.gold = gold;
            this.spender = spender;
            this.goldItems = goldItems;
            this.events = events;
            this.uiManager = uiManager;
            this.runner = runner;
            this.pacing = pacing ?? new RestPacingSettings();

            elements.StopButton.onClick.RemoveAllListeners();
            elements.StopButton.onClick.AddListener(StopRest);
            // 확인은 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 채널이 숙소로 되돌린다.
            elements.ConfirmButton.onClick.RemoveAllListeners();
            elements.ConfirmButton.onClick.AddListener(() => uiManager.Close(PanelId));
            if (gold != null) gold.Changed += HandleGoldChanged;
            elements.Root.SetActive(false);
        }

        /// <summary>숙소의 "휴식"에서 부른다.</summary>
        public void Begin(int days)
        {
            flow?.Abort();   // 끝나지 않은 이전 휴식이 남았으면(정상 경로엔 없음) 먼저 끝낸다
            if (flow != null) flow.Changed -= HandleFlowChanged;
            costPerDay = rest.CostPerDay;
            run = new RestProgressRun(days, RestPacing.SecondsPerDay(days, pacing));
            flow = new InnRestDayFlow(days, rest, events);
            flow.Changed += HandleFlowChanged;
            flow.Start();
            uiManager.Open(PanelId);
        }

        public void Open()
        {
            if (flow == null) return;

            isOpen = true;
            resting.Set(true);
            ApplyVisibility();
            Render();
            StopTicking();
            if (flow.Phase != RestDayPhase.Ended) ticking = runner.StartCoroutine(Tick());
        }

        // Close()는 표시/숨김만 한다. 확인 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        // 진행 중에 다른 경로로 닫히면(씬 전환 등) 성립한 일수까지만 휴식한 것으로 끝낸다 - 진행 중 이벤트도 중단된다.
        public void Close()
        {
            isOpen = false;
            StopTicking();
            flow?.Abort();
            resting.Set(false);
            elements.Root.SetActive(false);
        }

        public void Dispose()
        {
            isOpen = false;
            StopTicking();
            flow?.Abort();
            if (flow != null) flow.Changed -= HandleFlowChanged;
            if (gold != null) gold.Changed -= HandleGoldChanged;
            resting.Set(false);
        }

        private IEnumerator Tick()
        {
            while (flow.Phase != RestDayPhase.Ended)
            {
                yield return null;
                if (flow.Phase != RestDayPhase.Waiting) continue;   // 이벤트·하루 처리 중엔 게이지 정지
                if (run.IsDayReady) run.BeginNextDay(flow.SettledDays);
                if (run.Advance(Time.unscaledDeltaTime)) flow.OnWaitFinished();
                RenderGauge();
            }
            ticking = null;
        }

        private void StopRest() => flow?.Stop();

        private void HandleFlowChanged()
        {
            if (flow.Phase == RestDayPhase.Ended)
            {
                StopTicking();
                // 미성립 날의 대기는 휴식에 넣지 않는다 - 게이지를 성립 일수에 맞춘다.
                run.BeginNextDay(flow.SettledDays);
                run.Stop();
            }
            if (!isOpen) return;
            ApplyVisibility();
            Render();
        }

        // 이벤트 동안은 진행 화면을 숨겼다가 끝나면 다시 보인다(사용자 결정 2026-10-29) - 이벤트 대화·선택 화면 뒤로 진행 화면이 비쳐 보이지 않게.
        // 패널을 닫는 것이 아니라 시각 루트만 끈다 - Close()는 휴식을 중단시키고, 재화 HUD 입력 차단(RestingCondition)도 이벤트 중에 유지돼야 한다.
        // 코루틴은 Bootstrap 실행 주체에서 돌아 루트를 꺼도 멈추지 않는다.
        private void ApplyVisibility()
        {
            if (elements.Root == null) return; // Hub 언로드 뒤
            elements.Root.SetActive(!flow.IsEventRunning);
        }

        private void HandleGoldChanged()
        {
            if (isOpen) RenderTexts();
        }

        private void Render()
        {
            RenderGauge();
            RenderTexts();
        }

        private void RenderGauge()
        {
            var fill = elements.GaugeFill;
            if (fill == null || run == null) return; // Hub 언로드 뒤
            fill.anchorMax = new Vector2(run.Progress, fill.anchorMax.y);
        }

        private void RenderTexts()
        {
            if (elements.Root == null || flow == null) return; // Hub 언로드 뒤

            var settled = flow.SettledDays;
            elements.DaysLabel.text = $"{settled} / 총 {flow.PlannedDays}일";
            elements.DateLabel.text = "날짜 " + (date != null ? date.Today.ToDisplayString() : MissingText);
            elements.CostLabel.text = $"누적 비용 {flow.PaidTotal:N0}";
            RenderForecast(settled);
            elements.EventLogLabel.text = BuildLog();

            var ended = flow.Phase == RestDayPhase.Ended;
            elements.ResultLabel.text = ended ? ResultText() : string.Empty;
            elements.StopButton.gameObject.SetActive(!ended && flow.CanStop);
            elements.ConfirmButton.gameObject.SetActive(ended);
        }

        private void RenderForecast(int settled)
        {
            if (gold == null)
            {
                elements.GoldLabel.text = "예상 남은 개인 골드 " + MissingText;
                elements.BoxGoldLabel.text = "예상 남은 적재 골드 " + MissingText;
                return;
            }
            var remaining = flow.Phase == RestDayPhase.Ended ? 0 : (flow.PlannedDays - settled) * costPerDay;
            var boxes = remaining > 0 ? spender.PreviewWithdrawal(remaining).Count : 0;
            var (personal, loaded, insufficient) = RestGoldForecast.Compute(gold.PersonalGold, gold.LoadedGold, remaining, boxes, goldItems?.GoldValuePerItem ?? 0);
            elements.GoldLabel.text = insufficient ? "예상 남은 개인 골드 부족" : $"예상 남은 개인 골드 {personal:N0}";
            elements.BoxGoldLabel.text = $"예상 남은 적재 골드 {loaded:N0}";
        }

        // 한 줄 = "n일차 제목 — 결과 요약"(요약 없으면 제목만), 최근 MaxLogLines줄.
        private string BuildLog()
        {
            log.Clear();
            var records = flow.Records;
            for (var i = Math.Max(0, records.Count - MaxLogLines); i < records.Count; i++)
            {
                var r = records[i];
                if (log.Length > 0) log.Append('\n');
                log.Append(r.Day).Append("일차 ").Append(string.IsNullOrEmpty(r.Title) ? MissingText : r.Title);
                if (!string.IsNullOrEmpty(r.ResultSummary)) log.Append(" — ").Append(r.ResultSummary);
            }
            return log.ToString();
        }

        // 결과는 별도 창이 아니라 같은 화면에서 보인다(기획 94 4-13) - 게이지 정지, "휴식 중단" 자리에 "확인".
        private string ResultText()
        {
            var n = flow.SettledDays;
            var cost = flow.PaidTotal;
            return flow.EndReason switch
            {
                RestEndReason.Completed => $"{n}일 휴식 완료, 비용 {cost:N0}",
                RestEndReason.ForcedByEvent => $"{n}일 휴식 후 이벤트로 종료, 비용 {cost:N0}",
                RestEndReason.InsufficientGold => $"골드 부족으로 {n}일에서 종료, 비용 {cost:N0}",
                _ => $"{n}일 휴식 중단, 비용 {cost:N0}",
            };
        }

        private void StopTicking()
        {
            if (ticking == null) return;
            if (runner != null) runner.StopCoroutine(ticking);
            ticking = null;
        }
    }
}
