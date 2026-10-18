#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 이벤트 강제 발생·상태·도로 난이도 표시(변경 항목별 사건 Id)와 조작(사건 Id `debug`)·플래그 조작(설계 89 §7). Field 화면에서만 그린다. 접힌 버튼은 대화 디버그 버튼 바로 아래
    /// (오른쪽 위), 펼친 창은 이력·대화 창과 같은 자리이고 셋 중 하나만 펼쳐진다(NarrativeDebugPanels). 강제 발생은 조건·횟수를 무시하고, 정비창이 열려 있으면
    /// 평소처럼 기다린다. 도로 난이도 조작은 이벤트 효과와 같은 적용 경로(EventRoadDifficultyEffects)를 쓴다 - 디버그 결과가 실제 효과와 달라지지 않게.
    /// 예시 콘텐츠에 없는 기간·영구 방식을 Play에서 확인하는 용도다. 플래그는 이력에 바로 쓴다(Yarn 변수·조건 flag가 같은 원천).
    /// 이벤트 중(전투 제외)에는 그리지 않는다 - 이벤트 화면과 겹치지 않게(§13, 2026-10-16 검진).
    /// FieldManager와 같은 GameObject(Bootstrap 상주)에 붙고 EventDebugInstaller가 설치/제거한다.
    /// 걷어낼 때는 Remove 메뉴 → 이 파일·설치기 삭제 → NarrativeDebugPanels의 Event 상수·DebugToolsBulkInstaller 두 줄 삭제.
    /// </summary>
    public class EventDebugView : MonoBehaviour
    {
        private const float ToggleWidth = 90f;
        private const float ToggleHeight = 26f;
        private const float Margin = 8f;
        private const float ToggleTop = Margin + (ToggleHeight + 4f) * 2f;
        private const float PanelWidth = 340f;
        private const float PanelHeight = 480f;
        private const float PanelTopRatio = 0.10f;
        private const int DifficultyStep = 10;
        private const int TimedDays = 3;

        private IEventEncounterDebug events;
        private ITripCheckSignal checks;
        private IEncounterContentReader content;
        private ICurrentLegDifficulty leg;
        private ICityPairDifficultyEditor pairs;
        private IDateReader date;
        private IEncounterHistoryReader historyReader;
        private IEncounterHistoryWriter historyWriter;
        private EventRoadDifficultyEffects roadEffects;
        private string flagName = "helped_cart";
        private string lastResult = string.Empty;
        private bool isFieldLoaded;
        private IFieldUIHideReader uiHide; // 이벤트 중 그리지 않는다(설계 89 §13) - 없으면 항상 그림
        private Vector2 scroll;
        private readonly StringBuilder status = new();

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies == null) return;
            dependencies.TryResolve(out events);
            dependencies.TryResolve(out checks);
            dependencies.TryResolve(out content);
            dependencies.TryResolve(out leg);
            dependencies.TryResolve(out pairs);
            dependencies.TryResolve(out date);
            dependencies.TryResolve(out historyReader);
            dependencies.TryResolve(out historyWriter);
            dependencies.TryResolve(out uiHide);
            if (leg != null) roadEffects = new EventRoadDifficultyEffects(leg, pairs, date, () => CityPairDifficultyIds.DebugSource);
        }

        private void OnEnable()
        {
            isFieldLoaded = SceneManager.GetSceneByName(SceneNames.Field).isLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Field) isFieldLoaded = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name == SceneNames.Field) isFieldLoaded = false;
        }

        private void OnGUI()
        {
            if (!isFieldLoaded) return;
            if (uiHide != null && uiHide.IsHidden) return;

            var expanded = NarrativeDebugPanels.Expanded == NarrativeDebugPanels.Event;
            var toggle = new Rect(Screen.width - ToggleWidth - Margin, ToggleTop, ToggleWidth, ToggleHeight);
            if (GUI.Button(toggle, expanded ? "이벤트 닫기" : "이벤트")) NarrativeDebugPanels.Toggle(NarrativeDebugPanels.Event);
            if (NarrativeDebugPanels.Expanded != NarrativeDebugPanels.Event) return;

            var area = new Rect(Screen.width - PanelWidth - Margin, Screen.height * PanelTopRatio, PanelWidth, PanelHeight);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            GUILayout.Label(DescribeStatus());
            DrawDifficultyControls();
            DrawFlagControls();
            scroll = GUILayout.BeginScrollView(scroll);
            if (events != null && content != null)
            {
                foreach (var entry in content.Events)
                {
                    if (GUILayout.Button($"{entry.Id} ({entry.Occurrence}, {entry.Importance})")) events.DebugStart(entry.Id);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 3방식 × 증가·감소 = 6개. 진행 중 구간이 없으면 누를 수 없다.
        private void DrawDifficultyControls()
        {
            GUI.enabled = roadEffects != null && leg.HasLeg;
            DifficultyRow("이번 구간", EventRoadDifficultyEffects.LegMode, 0);
            DifficultyRow($"{TimedDays}일", EventRoadDifficultyEffects.DaysMode, TimedDays);
            DifficultyRow("영구", EventRoadDifficultyEffects.PermanentMode, 0);
            GUI.enabled = true;
            if (lastResult.Length > 0) GUILayout.Label("마지막 적용: " + lastResult);
        }

        private void DifficultyRow(string label, string mode, int days)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70f));
            if (GUILayout.Button($"+{DifficultyStep}")) ApplyDifficulty(DifficultyStep, mode, days);
            if (GUILayout.Button($"−{DifficultyStep}")) ApplyDifficulty(-DifficultyStep, mode, days);
            GUILayout.EndHorizontal();
        }

        private void ApplyDifficulty(int change, string mode, int days)
        {
            if (roadEffects == null || !roadEffects.TryApply(change, mode, days, out var text)) return;
            lastResult = text;
            Debug.Log($"[이벤트 디버그] {text}");
        }

        private void DrawFlagControls()
        {
            GUILayout.BeginHorizontal();
            flagName = GUILayout.TextField(flagName ?? string.Empty, GUILayout.Width(150f));
            GUI.enabled = historyWriter != null && !string.IsNullOrWhiteSpace(flagName);
            if (GUILayout.Button("켜기")) historyWriter.SetFlag(flagName.Trim(), true);
            if (GUILayout.Button("끄기")) historyWriter.SetFlag(flagName.Trim(), false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            var value = historyReader != null && historyReader.TryGetFlag(flagName?.Trim(), out var flag) ? flag.ToString() : "미설정";
            GUILayout.Label($"플래그 {flagName} = {value}");
        }

        private string DescribeStatus()
        {
            status.Clear();
            if (events == null) return "이벤트 진행자 없음(Build Bootstrap Scene)";
            if (checks != null) status.AppendLine($"다음 이벤트 판정 {checks.SecondsUntil(TripCheckKind.Event):0.0}s");
            status.AppendLine("진행 중: " + (events.IsRunning ? events.CurrentEventId : "없음") + (events.PendingEventId != null ? $" / 대기: {events.PendingEventId}" : ""));
            status.Append("확정 대기열: ");
            for (var i = 0; i < events.QueuedEvents.Count; i++) status.Append(i == 0 ? "" : ", ").Append(events.QueuedEvents[i].Id).Append('(').Append(events.QueuedEvents[i].Importance).Append(')');
            status.AppendLine();
            if (leg != null && leg.HasLeg)
            {
                var pair = leg.CurrentPair;
                status.Append($"도로 {pair}: 기본 {leg.CurrentBaseDifficulty} / 이번 구간 {leg.LegOnlyChange:+0;-0;0} / 현재 {leg.CurrentDifficulty}({leg.CurrentGradeName})");
                if (pairs != null)
                {
                    foreach (var change in pairs.GetChanges(pair))
                    {
                        status.AppendLine().Append("  ").Append(change.Id).Append(change.IsTimed ? " " : " 영구 ").Append(change.Amount.ToString("+0;-0;0"));
                        if (change.IsTimed && date != null) status.Append($" ({change.ExpiresOnDay.Value - date.Today.TotalDays}일 남음)");
                    }
                }
            }
            return status.ToString();
        }
    }
}
#endif
