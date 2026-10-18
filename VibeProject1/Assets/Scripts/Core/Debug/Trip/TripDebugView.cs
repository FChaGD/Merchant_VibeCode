#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - Field 화면 좌상단, 전투 강제 승패 버튼(BattleDebugResultView, y 10~80) 아래에 상행 디버그 버튼을 그린다(OnGUI, Docs/설계/76번 §8).
    /// 1줄: 시간 배속(1× / 10× / 60×). 2줄: 인카운터 즉시 발생 / 상행 즉시 완료. 긴 거리 구간(15분 이상)·난이도별 전투를 검증하는 부담을 줄인다.
    /// 3줄: 게임 날짜 진행(구간 경과·소요, 상행 누적 이동 초, 다음 날까지 남은 초, 궤주 시 추가 일수, 설계 93번 §6).
    /// - 상행 완료는 남은 이동 초를 날짜 시계에 넣은 뒤 끝낸다 - 정상 완료와 같은 누적 규칙으로 날짜가 흐른다(사용자 결정 2026-10-11, 설계 93번 §6).
    /// - 배속: Time.timeScale을 바꾼다 - 진행·인카운터 판정·정비창 타이머·전투가 같은 비율로 빨라진다. Field를 벗어나면 1×로 되돌린다.
    /// - 인카운터: EncounterManager의 에디터 전용 진입점으로 정상 인카운터 흐름(경고 → 전투)을 그대로 탄다(적 구성도 평소와 같다).
    /// - 상행 완료: 여정을 마지막 구간으로 넘기고 그 구간을 즉시 끝낸다 - 최종 도착 흐름(현재 위치 = 도착지, 도착 팝업)이 그대로 실행된다.
    /// 두 버튼은 이동 중에만 누를 수 있다 - 진행 이벤트(OnProgressChanged)가 오는 동안을 이동 중으로, 인카운터 발생부터 다음 진행 이벤트까지를
    /// 전투 중으로 본다(전투 중 상행 완료가 눌리면 전투 위에서 도착 처리가 겹친다).
    /// GameManager와 같은 GameObject(Bootstrap 상주)에 붙고 TripDebugInstaller가 설치/제거한다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤 이 파일·TripDebugInstaller.cs(+.meta)·EncounterManager의 에디터 전용 진입점을 지우고
    /// DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public class TripDebugView : MonoBehaviour
    {
        private static readonly float[] Scales = { 1f, 10f, 60f };
        // BattleDebugResultView가 (10, 10)부터 높이 70을 쓴다 - 그 아래에 둔다.
        private const float Left = 10f;
        private const float Top = 90f;
        private const float ButtonWidth = 72f;
        private const float WideButtonWidth = 110f;
        private const float ButtonHeight = 28f;
        private const float Gap = 4f;
        // 상행 완료로 남은 구간을 넘길 때 마지막 구간에 주는 시간 - 다음 프레임에 바로 도착 처리된다.
        private const float InstantArrivalSeconds = 0.01f;
        private const float AccrualLabelWidth = 420f; // 최소 폭 - 날짜 진행 항목이 늘어 글이 길면 글 폭에 맞춰 넓힌다(설계 93 §6)

        private ISessionState sessionState;
        private EncounterManager encounterManager;
        private ITripItinerary itinerary;
        private bool isFieldLoaded;
        private bool isMoving;
        private IFieldUIHideReader uiHide; // 이벤트 중 그리지 않는다(설계 89 §13) - 없으면 항상 그림
        private ITripDayClockDebug dayClock; // 날짜 진행 표시·상행 완료 남은 이동 초(설계 93 §6) - 없으면 날짜 없이 기존 동작
        private string dayPreviewText; // 3줄 캐시 - 표시 값이 바뀔 때만 다시 만든다
        private float dayPreviewWidth;
        private (int, int, int, int, int) dayPreviewKey;

        private void Awake()
        {
            sessionState = GetComponent<ISessionState>();
            encounterManager = FindFirstObjectByType<EncounterManager>(FindObjectsInactive.Include);
            itinerary = FindFirstObjectByType<TripItineraryState>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies == null) return;
            dependencies.TryResolve(out uiHide);
            dependencies.TryResolve(out dayClock);
        }

        private void OnEnable()
        {
            isFieldLoaded = SceneManager.GetSceneByName(SceneNames.Field).isLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            if (sessionState != null) sessionState.OnProgressChanged += HandleProgressChanged;
            if (encounterManager != null) encounterManager.OnEncounterTriggered += HandleEncounterTriggered;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (sessionState != null) sessionState.OnProgressChanged -= HandleProgressChanged;
            if (encounterManager != null) encounterManager.OnEncounterTriggered -= HandleEncounterTriggered;
            Time.timeScale = 1f;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Field) isFieldLoaded = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != SceneNames.Field) return;
            isFieldLoaded = false;
            isMoving = false;
            Time.timeScale = 1f; // 마을에서 배속이 남지 않게
        }

        // 진행 이벤트는 이동 중에만 온다(전투 중·도착 뒤엔 멈춘다). 도착 틱(1)은 이동 끝.
        private void HandleProgressChanged(float progress) => isMoving = progress < 1f;

        private void HandleEncounterTriggered() => isMoving = false;

        private void OnGUI()
        {
            if (!Application.isPlaying || !isFieldLoaded) return;
            if (uiHide != null && uiHide.IsHidden) return;

            var y = Top;
            for (var i = 0; i < Scales.Length; i++)
            {
                var label = Mathf.Approximately(Time.timeScale, Scales[i]) ? $"[{Scales[i]}×]" : $"{Scales[i]}×";
                if (GUI.Button(new Rect(Left + i * (ButtonWidth + Gap), y, ButtonWidth, ButtonHeight), label)) Time.timeScale = Scales[i];
            }

            y += ButtonHeight + Gap;
            var enabled = GUI.enabled;
            GUI.enabled = enabled && isMoving;
            if (GUI.Button(new Rect(Left, y, WideButtonWidth, ButtonHeight), "인카운터 발생") && encounterManager != null)
            {
                encounterManager.DebugTriggerEncounter();
            }
            if (GUI.Button(new Rect(Left + WideButtonWidth + Gap, y, WideButtonWidth, ButtonHeight), "상행 완료") && sessionState != null)
            {
                CompleteTrip();
            }
            GUI.enabled = enabled;

            y += ButtonHeight + Gap;
            if (sessionState != null)
            {
                RefreshDayPreview();
                GUI.Box(new Rect(Left, y, dayPreviewWidth, ButtonHeight), dayPreviewText);
            }
        }

        // 3줄: 날짜 진행 상태(설계 93번 §6) - 값은 실제 날짜를 진행하는 날짜 시계에서 그대로 읽는다. OnGUI는 한 프레임에 이벤트마다 여러 번 불리므로
        // 표시 단위(초 정수·0.1초·일)로 바뀐 값이 있을 때만 문자열을 조립하고 폭을 잰다 - 그 밖의 호출은 캐시를 그대로 그린다.
        private void RefreshDayPreview()
        {
            var key = (Round(sessionState.ElapsedSeconds), Round(sessionState.DurationSeconds),
                dayClock != null ? Round(dayClock.MovedSeconds) : -1, dayClock != null ? Round(dayClock.SecondsUntilNextDay * 10f) : -1,
                dayClock != null ? dayClock.RoutPreviewDays : -1);
            if (dayPreviewText != null && key == dayPreviewKey) return;
            dayPreviewKey = key;

            dayPreviewText = $"구간 경과 {key.Item1}초 / 소요 {key.Item2}초";
            if (dayClock != null) dayPreviewText += $" · 상행 이동 {key.Item3}초 · 다음 날까지 {key.Item4 / 10f:0.0}초 · 궤주 시 +{key.Item5}일";
            dayPreviewWidth = Mathf.Max(AccrualLabelWidth, GUI.skin.box.CalcSize(new GUIContent(dayPreviewText)).x);
        }

        private static int Round(float value) => (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);

        // 상행 완료도 정상 완료와 같은 날짜가 흘러야 한다(사용자 결정 2026-10-11, 설계 93번 §6). 건너뛰기 전에 남은 이동 초 - 현재 구간의 남은 시간
        // (지연·단축 반영) + 이후 구간 소요시간 - 를 날짜 시계에 넣어 나머지 누적 규칙을 그대로 탄다. 마지막 0.01초 구간의 이동분은 상행 종료 때 버려진다.
        // 버튼은 이동 중에만 눌리므로(구간 도착 알림 중엔 비활성) 이미 지나간 구간을 다시 더하지 않는다.
        private void CompleteTrip()
        {
            var remaining = Mathf.Max(0f, sessionState.DurationSeconds - sessionState.ElapsedSeconds);
            if (itinerary != null)
            {
                while (itinerary.HasNextLeg)
                {
                    itinerary.AdvanceLeg();
                    remaining += itinerary.CurrentLeg.DurationSeconds;
                }
            }
            dayClock?.DebugAddMoved(remaining);
            isMoving = false;
            sessionState.Begin(InstantArrivalSeconds);
        }
    }
}
#endif
