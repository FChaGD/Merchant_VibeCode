#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - Field 화면 좌상단, 전투 강제 승패 버튼(BattleDebugResultView, y 10~80) 아래에 상행 디버그 버튼을 그린다(OnGUI, Docs/설계/76번 §8).
    /// 1줄: 시간 배속(1× / 10× / 60×). 2줄: 인카운터 즉시 발생 / 상행 즉시 완료. 긴 거리 구간(15분 이상)·난이도별 전투를 검증하는 부담을 줄인다.
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

        private ISessionState sessionState;
        private EncounterManager encounterManager;
        private ITripItinerary itinerary;
        private bool isFieldLoaded;
        private bool isMoving;

        private void Awake()
        {
            sessionState = GetComponent<ISessionState>();
            encounterManager = FindFirstObjectByType<EncounterManager>(FindObjectsInactive.Include);
            itinerary = FindFirstObjectByType<TripItineraryState>(FindObjectsInactive.Include);
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
        }

        private void CompleteTrip()
        {
            if (itinerary != null)
            {
                while (itinerary.HasNextLeg) itinerary.AdvanceLeg();
            }
            isMoving = false;
            sessionState.Begin(InstantArrivalSeconds);
        }
    }
}
#endif
