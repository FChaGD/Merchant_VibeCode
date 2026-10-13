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

        private IEncounterHistoryReader history;
        private IEncounterHistoryWriter historyWriter;
        private ITripCheckSignal checks;
        private IDialogueScriptRunner scriptRunner;
        private readonly List<string> smokeLog = new();
        private bool expanded;
        private bool isFieldLoaded;
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
            }
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

            var toggle = new Rect(Screen.width - ToggleWidth - Margin, Margin, ToggleWidth, ToggleHeight);
            if (GUI.Button(toggle, expanded ? "이력 닫기" : "이력")) expanded = !expanded;
            if (!expanded) return;

            var area = new Rect(Screen.width - PanelWidth - Margin, Screen.height * PanelTopRatio, PanelWidth, PanelHeight);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            if (checks != null)
            {
                GUILayout.Label($"다음 판정 - 전투 {checks.SecondsUntil(TripCheckKind.Battle):0.0}s / 대화 {checks.SecondsUntil(TripCheckKind.Dialogue):0.0}s / 이벤트 {checks.SecondsUntil(TripCheckKind.Event):0.0}s");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이번 상행 초기화")) historyWriter?.ResetTrip();
            if (GUILayout.Button("전체 초기화")) historyWriter?.ResetAll();
            if (GUILayout.Button("대본 스모크")) RunSmoke();
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Describe());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
                sb.AppendLine("발생(이번 상행): " + string.Join(", ", history.TripOccurredIds));
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
