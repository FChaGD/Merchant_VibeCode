#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 이벤트·대화 이력과 판정 시계를 보여 주고, 이력을 초기화하고, 검증용 대본(Debug_Smoke)을 화면 없이 돌려 본다(설계 87 §8.2).
    /// Field 화면에서만 그린다 - Hub 오른쪽 위엔 재화 디버그 토글이 있다. 기본은 오른쪽 위 모서리의 접힌 버튼 하나(진행 게이지 x 15~85% 바깥)이고,
    /// 펼친 창은 게이지 아래(화면 높이 10%부터)에 연다. FieldManager와 같은 GameObject(Bootstrap 상주)에 붙고 EncounterDebugInstaller가 설치/제거한다.
    /// Hub에서는 휴식 이벤트 강제 지정 한 줄만 오른쪽 아래에 그린다(설계 109 §7) - 왼쪽 아래는 내구도 디버그 줄, 위쪽은 재화·날짜 디버그가 쓴다.
    /// 지정한 이벤트는 다음 휴식일 확정 점검에서 조건·횟수를 무시하고 시작한다(1회).
    /// 걷어낼 때는 Remove 메뉴 → 이 파일·DialogueRecorderTarget·설치기 삭제 → DebugToolsBulkInstaller 두 줄 삭제.
    /// </summary>
    public class EncounterDebugView : MonoBehaviour
    {
        private const float ToggleWidth = 90f;
        private const float ToggleHeight = 26f;
        private const float Margin = 8f;
        private const float PanelWidth = 340f;
        private const float PanelHeight = 300f;
        private const float PanelTopRatio = 0.10f;
        private const string SmokeNode = "Debug_Smoke";
        private const float RestRowHeight = 26f;
        private const float RestArrowWidth = 26f;
        private const float RestLabelWidth = 200f;
        private const float RestButtonWidth = 50f;
        private const float RestGap = 4f;

        private IEncounterHistoryReader history;
        private IEncounterHistoryWriter historyWriter;
        private ITripCheckSignal checks;
        private IDialogueScriptRunner scriptRunner;
        private readonly List<string> smokeLog = new();
        private bool isFieldLoaded;
        private IFieldUIHideReader uiHide; // 이벤트 중 그리지 않는다(설계 89 §13) - 없으면 항상 그림
        private IRestEventDebug restDebug;
        private bool isHubLoaded;
        private bool restExpanded;
        private int restIndex;
        private Vector2 scroll;

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies != null)
            {
                dependencies.TryResolve(out history);
                dependencies.TryResolve(out historyWriter);
                dependencies.TryResolve(out checks);
                dependencies.TryResolve(out scriptRunner);
                dependencies.TryResolve(out uiHide);
                dependencies.TryResolve(out restDebug);
            }
        }

        private void OnEnable()
        {
            isFieldLoaded = SceneManager.GetSceneByName(SceneNames.Field).isLoaded;
            isHubLoaded = SceneManager.GetSceneByName(SceneNames.Hub).isLoaded;
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
            if (scene.name == SceneNames.Hub) isHubLoaded = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name == SceneNames.Field) isFieldLoaded = false;
            if (scene.name == SceneNames.Hub) isHubLoaded = false;
        }

        private void OnGUI()
        {
            if (isHubLoaded) DrawRestRow();
            if (!isFieldLoaded) return;
            if (uiHide != null && uiHide.IsHidden) return;

            var toggle = new Rect(Screen.width - ToggleWidth - Margin, Margin, ToggleWidth, ToggleHeight);
            var expanded = NarrativeDebugPanels.Expanded == NarrativeDebugPanels.History;
            if (GUI.Button(toggle, expanded ? "이력 닫기" : "이력")) NarrativeDebugPanels.Toggle(NarrativeDebugPanels.History);
            if (NarrativeDebugPanels.Expanded != NarrativeDebugPanels.History) return;

            var area = new Rect(Screen.width - PanelWidth - Margin, Screen.height * PanelTopRatio, PanelWidth, PanelHeight);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            if (checks != null)
            {
                GUILayout.Label($"다음 판정 - 전투 {checks.SecondsUntil(TripCheckKind.Battle):0.0}s / 대화 {checks.SecondsUntil(TripCheckKind.Dialogue):0.0}s / 이벤트 {checks.SecondsUntil(TripCheckKind.Event):0.0}s");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이번 회차 초기화")) historyWriter?.ResetTrip();
            if (GUILayout.Button("전체 초기화")) historyWriter?.ResetAll();
            if (GUILayout.Button("대본 스모크")) RunSmoke();
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Describe());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // 오른쪽 끝부터 왼쪽으로: 토글 → [해제] [지정] → ▶ 대상 ◀. 대상 = 휴식 이벤트 목록(테이블 순서).
        private void DrawRestRow()
        {
            var y = Screen.height - RestRowHeight - Margin;
            var x = Screen.width - Margin - ToggleWidth;
            if (GUI.Button(new Rect(x, y, ToggleWidth, RestRowHeight), restExpanded ? "휴식 닫기" : "휴식 이벤트")) restExpanded = !restExpanded;
            if (!restExpanded) return;

            var events = restDebug != null ? restDebug.RestEvents : null;
            if (events == null || events.Count == 0)
            {
                x -= RestGap + RestLabelWidth;
                GUI.Box(new Rect(x, y, RestLabelWidth, RestRowHeight), restDebug == null ? "진행자 없음(Build Bootstrap)" : "휴식 이벤트 없음");
                return;
            }
            restIndex = Mathf.Clamp(restIndex, 0, events.Count - 1);
            var id = events[restIndex].Id;

            x -= RestGap + RestButtonWidth;
            if (GUI.Button(new Rect(x, y, RestButtonWidth, RestRowHeight), "해제")) restDebug.ForceNext(null);
            x -= RestGap + RestButtonWidth;
            if (GUI.Button(new Rect(x, y, RestButtonWidth, RestRowHeight), "지정")) restDebug.ForceNext(id);
            x -= RestGap + RestArrowWidth;
            if (GUI.Button(new Rect(x, y, RestArrowWidth, RestRowHeight), "▶")) restIndex = (restIndex + 1) % events.Count;
            x -= RestGap + RestLabelWidth;
            var forced = restDebug.ForcedNextId;
            GUI.Box(new Rect(x, y, RestLabelWidth, RestRowHeight), forced == null ? id : $"{id} (지정: {forced})");
            x -= RestGap + RestArrowWidth;
            if (GUI.Button(new Rect(x, y, RestArrowWidth, RestRowHeight), "◀")) restIndex = (restIndex - 1 + events.Count) % events.Count;
        }

        // 화면 없이 검증용 노드를 끝까지 흘려 Yarn 연결(변수 저장소·표시기·실행기)을 확인한다. 실행 중인 대본이 있으면 건드리지 않는다.
        private void RunSmoke()
        {
            smokeLog.Clear();
            if (scriptRunner == null)
            {
                smokeLog.Add("대본 실행기 없음(Build Bootstrap Scene)");
                return;
            }
            if (!scriptRunner.TryRun(SmokeNode, new DialogueRecorderTarget(smokeLog), completed => smokeLog.Add($"완료={completed}")))
            {
                smokeLog.Add($"시작 실패 - 실행 중이거나 노드 '{SmokeNode}' 없음");
            }
        }

        private string Describe()
        {
            var sb = new StringBuilder();
            if (history != null)
            {
                sb.AppendLine("플래그: " + string.Join(", ", history.ActiveFlags));
                sb.AppendLine("발생(전체): " + string.Join(", ", history.OccurredIds));
                sb.AppendLine("발생(이번 회차): " + string.Join(", ", history.TripOccurredIds));
            }
            if (smokeLog.Count > 0)
            {
                sb.AppendLine("대본 스모크:");
                foreach (var entry in smokeLog) sb.AppendLine("  " + entry);
            }
            return sb.ToString();
        }
    }
}
#endif
