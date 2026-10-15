#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 상행 대화 강제 발생·상태 표시(설계 88 §8). Field 화면에서만 그린다. 접힌 버튼은 이력 버튼 바로 아래(오른쪽 위, 진행 게이지 x 15~85% 바깥),
    /// 펼친 창은 이력 창과 같은 자리이고 둘 중 하나만 펼쳐진다(NarrativeDebugPanels). 강제 발생은 조건·횟수·화자 고용을 무시하고, 진행 중 대화가 있으면 끝낸 뒤 시작한다.
    /// FieldManager와 같은 GameObject(Bootstrap 상주)에 붙고 DialogueDebugInstaller가 설치/제거한다.
    /// 걷어낼 때는 Remove 메뉴 → 이 파일·NarrativeDebugPanels(이력 디버그도 걷어낼 때)·설치기 삭제 → DebugToolsBulkInstaller 두 줄 삭제.
    /// </summary>
    public class DialogueDebugView : MonoBehaviour
    {
        private const float ToggleWidth = 90f;
        private const float ToggleHeight = 26f;
        private const float Margin = 8f;
        private const float ToggleTop = Margin + ToggleHeight + 4f;
        private const float PanelWidth = 340f;
        private const float PanelHeight = 300f;
        private const float PanelTopRatio = 0.10f;

        private ITripDialogueDebug dialogue;
        private ITripCheckSignal checks;
        private IEncounterContentReader content;
        private bool isFieldLoaded;
        private Vector2 scroll;
        private readonly StringBuilder status = new();

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies == null) return;
            dependencies.TryResolve(out dialogue);
            dependencies.TryResolve(out checks);
            dependencies.TryResolve(out content);
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

            var expanded = NarrativeDebugPanels.Expanded == NarrativeDebugPanels.Dialogue;
            var toggle = new Rect(Screen.width - ToggleWidth - Margin, ToggleTop, ToggleWidth, ToggleHeight);
            if (GUI.Button(toggle, expanded ? "대화 닫기" : "대화")) NarrativeDebugPanels.Toggle(NarrativeDebugPanels.Dialogue);
            if (NarrativeDebugPanels.Expanded != NarrativeDebugPanels.Dialogue) return;

            var area = new Rect(Screen.width - PanelWidth - Margin, Screen.height * PanelTopRatio, PanelWidth, PanelHeight);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f));
            GUILayout.Label(DescribeStatus());
            scroll = GUILayout.BeginScrollView(scroll);
            if (dialogue != null && content != null)
            {
                foreach (var entry in content.Dialogues)
                {
                    if (GUILayout.Button($"{entry.Id} ({entry.Occurrence})")) dialogue.DebugStart(entry.Id);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string DescribeStatus()
        {
            status.Clear();
            if (dialogue == null) return "대화 진행자 없음(Build Bootstrap Scene)";
            if (checks != null) status.AppendLine($"다음 대화 검사 {checks.SecondsUntil(TripCheckKind.Dialogue):0.0}s");
            status.AppendLine("진행 중: " + (dialogue.IsRunning ? dialogue.CurrentDialogueId : "없음"));
            status.Append("쌓인 일: ");
            for (var i = 0; i < dialogue.PendingTriggers.Count; i++) status.Append(i == 0 ? "" : ", ").Append(dialogue.PendingTriggers[i]);
            if (dialogue.AfterBattleSecondsRemaining >= 0f) status.AppendLine().Append($"전투 후 대화까지 {dialogue.AfterBattleSecondsRemaining:0.0}s");
            return status.ToString();
        }
    }
}
#endif
