#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - Field 화면 좌상단, 전투 강제 승패 버튼(BattleDebugResultView, y 10~80) 아래에 상행 디버그 버튼을 그린다(OnGUI, Docs/설계/76번 §8).
    /// 1줄: 시간 배속(1× / 10× / 60×). 2줄: 인카운터 즉시 발생 / 상행 즉시 완료. 긴 거리 구간(15분 이상)·난이도별 전투를 검증하는 부담을 줄인다.
    /// 3줄: 게임 날짜 정산 미리보기(구간 경과·소요, 도착 시·궤주 시 가산 일수, Docs/설계/85번 §5).
    /// - 상행 완료는 정상 완료와 같은 일수를 날짜에 더한 뒤 끝낸다(사용자 결정 2026-10-11).
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
        private const float AccrualLabelWidth = 420f;

        private ISessionState sessionState;
        private EncounterManager encounterManager;
        private ITripItinerary itinerary;
        private DateManager dateManager; // 상행 완료 시 남은 일수 가산 - 없으면 날짜 없이 기존 동작
        private bool isFieldLoaded;
        private bool isMoving;
        private IFieldUIHideReader uiHide; // 이벤트 중 그리지 않는다(설계 89 §13) - 없으면 항상 그림

        private void Awake()
        {
            sessionState = GetComponent<ISessionState>();
            encounterManager = FindFirstObjectByType<EncounterManager>(FindObjectsInactive.Include);
            itinerary = FindFirstObjectByType<TripItineraryState>(FindObjectsInactive.Include);
            dateManager = FindFirstObjectByType<DateManager>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            var dependencies = FindFirstObjectByType<DependencyManager>(FindObjectsInactive.Include);
            if (dependencies != null) dependencies.TryResolve(out uiHide);
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
            if (sessionState != null) GUI.Box(new Rect(Left, y, AccrualLabelWidth, ButtonHeight), AccrualPreview());
        }

        // 3줄: 현재 구간 정산 미리보기(Docs/설계/85번 §5) - 도착·궤주 때 실제 가산과 같은 환산(TravelDayRules)을 쓴다.
        private string AccrualPreview()
        {
            var settings = TravelSettings;
            return $"구간 경과 {sessionState.ElapsedSeconds:0}초 / 소요 {sessionState.DurationSeconds:0}초 · 도착 시 +{TravelDayRules.ToDays(settings, sessionState.DurationSeconds)}일 · 궤주 시 +{TravelDayRules.ToRoutDays(settings, sessionState.ElapsedSeconds)}일";
        }

        // 여정 상태가 이동 수치 조회 계약도 구현한다(TripItineraryState) - 못 찾으면 기본값.
        private TripTravelSettings TravelSettings => (itinerary as ITripTravelSettingsReader)?.Settings ?? TripTravelSettings.Default();

        // 상행 완료도 정상 완료와 같은 일수가 흘러야 한다(사용자 결정 2026-10-11, 설계 85번 §5). 건너뛰기 전에 남은 일수를 더한다 -
        // 현재 구간은 소요시간 전체(파괴 지연 반영), 이후 구간은 각 구간 예상 일수. 마지막 구간은 0.01초로 끝나 도착 정산이 0일이라
        // 이중 가산이 없다. 버튼은 이동 중에만 눌리므로(구간 도착 알림 중엔 비활성) 이미 정산된 구간을 다시 더하지 않는다.
        private void CompleteTrip()
        {
            var days = TravelDayRules.ToDays(TravelSettings, sessionState.DurationSeconds);
            if (itinerary != null)
            {
                while (itinerary.HasNextLeg)
                {
                    itinerary.AdvanceLeg();
                    days += itinerary.CurrentLeg.EstimatedDays;
                }
            }
            if (dateManager != null) dateManager.AdvanceDays(days);
            isMoving = false;
            sessionState.Begin(InstantArrivalSeconds);
        }
    }
}
#endif
