#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 우상단 [재화] 토글 버튼으로 재화 패널 왼쪽 같은 줄에 "[-1000] [-100] 현재 금액 [+100] [+1000]" 한 줄을
    /// 화면 위에서 내렸다 올린다(OnGUI).
    /// 무역품 구매의 재화 부족/잔여 같은 경계 상황을 실행 중에 바로 만들기 위한 도구(Docs/기획/51번,
    /// Docs/설계/52번). 재화 지갑과 같은 GameObject(Bootstrap 씬 상주)에 부착된 형제 컴포넌트지만 마을(Hub)이
    /// 로드된 동안에만 그린다. 전역 DI 대상이 아니고(Awake에서 같은 오브젝트의 지갑 인터페이스를 직접 조회),
    /// 지갑 계약에 디버그 전용 기능을 추가하지 않는다 - 게임 코드에 흔적을 남기지 않아야 이 파일을 지우는
    /// 것만으로 걷어낼 수 있다. 설치/제거는 PlayerCurrencyDebugInstaller가 담당한다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 한 번 실행한 뒤 이 파일과 PlayerCurrencyDebugInstaller.cs(+.meta)를
    /// 지우고 DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public class PlayerCurrencyDebugSpinView : MonoBehaviour
    {
        // HubSceneInstaller.BuildPlayerCurrencyHud의 재화 패널 배치(화면 우측 30%, 우상단 여백 32px)와 맞춘 값.
        // Hub/Field 캔버스가 ConstantPixelSize(scaleFactor 1)라 OnGUI 픽셀 좌표와 단위가 같다. 게임 코드와
        // 상수를 공유하지 않는 이유는 이 디버그 코드를 걷어낼 때 게임 코드를 고치지 않기 위해서다 - 어긋나도
        // 버튼 위치만 틀어진다.
        private const float HudWidthRatio = 0.3f;
        private const float HudMarginRight = 32f;
        private const float HudMarginTop = 32f;
        // 재화 패널 높이는 콘텐츠 맞춤이라 추정값(아이콘 32 + 상하 패딩 8) - 겹치면 이 값만 조정한다.
        private const float HudEstimatedHeight = 40f;
        // 줄은 재화 패널 왼쪽, 같은 높이에 펼친다. 패널 아래는 호버 툴팁 띠(설계 83번 §6.1)와 상단 물류품 버튼이 빈틈없이
        // 이어져 놓을 자리가 없다(2026-10-10 실측, 1920x1080). OnGUI는 uGUI 위에 그려져 겹치면 아래 UI가 가려진다.
        // 이 자리는 날짜 패널(상단 중앙)도 덮으므로 평소엔 화면 위쪽 밖에 숨겨 두고 토글 버튼으로 내렸다 올린다 - 디버그 UI와
        // 게임 UI가 겹치면 디버그 UI를 옮긴다(사용자 지시, 2026-10-11). 배경은 아래 UI와 섞여 보이지 않게 검은 불투명이다.
        // 배치(Formation) 화면 상단 디버그 패널과도 겹쳐서 그 패널이 보이는 동안은 줄을 숨긴다(사용자 결정, 2026-10-10).
        private const float RowGap = 8f;
        private const float RowHeight = 32f;
        private const float CellPadding = 2f;
        private const int FontSize = 16;
        private const int SmallStep = 100;
        private const int LargeStep = 1000;
        private const float SlideSeconds = 0.2f;
        // 토글 버튼은 우상단 모서리(재화 패널 위 여백) - 마을 화면 전 상태·1920x1080/1280x720에서 어떤 UI와도 겹치지 않는
        // 유일한 자리였다(2026-10-11 실측).
        private const float ToggleWidth = 64f;
        private const float ToggleHeight = 24f;
        private const float ToggleMarginRight = 8f;
        private const float ToggleMarginTop = 4f;

        private IPlayerCurrencyWallet wallet;
        private bool isHubLoaded;
        private GameObject formationDebugPanel;
        private GUIStyle buttonStyle;
        private GUIStyle amountStyle;
        private GUIStyle rowStyle;
        private Texture2D rowBackground;
        private bool isOpen;
        private float slide; // 0 = 화면 밖, 1 = 펼침

        private void Awake()
        {
            wallet = GetComponent<IPlayerCurrencyWallet>();
            if (wallet == null)
            {
                Debug.LogWarning($"{nameof(PlayerCurrencyDebugSpinView)}: 같은 오브젝트에서 재화 지갑을 찾지 못해 버튼을 그리지 않는다.", this);
            }
        }

        // 마을(Hub)에서만 보이는 도구다(사용자 지시, 2026-09-28) - 지갑은 Bootstrap에 상주하므로 부착 위치로는
        // 씬을 가를 수 없어 Hub 로드 여부를 따로 추적한다. OnGUI는 프레임당 여러 번 불리므로 매번 씬을 조회하지
        // 않고 로드/언로드 이벤트로 갱신한다.
        private void OnEnable()
        {
            var hub = SceneManager.GetSceneByName(SceneNames.Hub);
            isHubLoaded = hub.isLoaded;
            if (isHubLoaded) CacheFormationDebugPanel(hub);
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
            if (scene.name != SceneNames.Hub) return;
            isHubLoaded = true;
            CacheFormationDebugPanel(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != SceneNames.Hub) return;
            isHubLoaded = false;
            formationDebugPanel = null;
        }

        // OnGUI에서 매번 UI 트리를 훑지 않도록 Hub 로드 시 한 번만 찾는다. 못 찾으면(배치 UI 미설치) 숨김 조건 없이 그린다.
        private void CacheFormationDebugPanel(Scene hub)
        {
            formationDebugPanel = null;
            foreach (var root in hub.GetRootGameObjects())
            {
                var sceneUIRoot = root.GetComponentInChildren<SceneUIRoot>(true);
                if (sceneUIRoot != null && sceneUIRoot.TryGetElement<RectTransform>(FormationUIElementIds.DebugPanelRoot, out var panel))
                {
                    formationDebugPanel = panel.gameObject;
                    return;
                }
            }
        }

        // 슬라이드는 OnGUI가 아니라 여기서 진행한다 - OnGUI는 이벤트마다 여러 번 불리고, 에디터 Game 뷰는 포커스가 없으면
        // 다시 그리지 않아 그리기 횟수에 묶으면 속도가 들쭉날쭉하다. 배속(timeScale)과 무관하게 같은 속도로 움직인다.
        private void Update()
        {
            var target = isOpen ? 1f : 0f;
            if (slide != target) slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / SlideSeconds);
        }

        private void OnDestroy()
        {
            if (rowBackground != null) Destroy(rowBackground);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || wallet == null || !isHubLoaded) return;
            EnsureStyles();

            var toggle = new Rect(Screen.width - ToggleMarginRight - ToggleWidth, ToggleMarginTop, ToggleWidth, ToggleHeight);
            if (GUI.Button(toggle, isOpen ? "재화 ▲" : "재화 ▼")) isOpen = !isOpen;

            if (slide <= 0f) return;
            if (formationDebugPanel != null && formationDebugPanel.activeInHierarchy) return;

            var width = Screen.width * HudWidthRatio;
            var shownY = HudMarginTop + (HudEstimatedHeight - RowHeight) / 2f;
            var y = Mathf.Lerp(-RowHeight, shownY, slide);
            var row = new Rect(Screen.width - HudMarginRight - width - RowGap - width, y, width, RowHeight);
            GUI.Box(row, GUIContent.none, rowStyle);

            var cellWidth = width / 5f;
            if (GUI.Button(Cell(row, cellWidth, 0), $"-{LargeStep}", buttonStyle)) Decrease(LargeStep);
            if (GUI.Button(Cell(row, cellWidth, 1), $"-{SmallStep}", buttonStyle)) Decrease(SmallStep);
            GUI.Label(Cell(row, cellWidth, 2), wallet.CurrentAmount.ToString(), amountStyle);
            if (GUI.Button(Cell(row, cellWidth, 3), $"+{SmallStep}", buttonStyle)) wallet.Add(SmallStep);
            if (GUI.Button(Cell(row, cellWidth, 4), $"+{LargeStep}", buttonStyle)) wallet.Add(LargeStep);
        }

        // 지갑의 차감은 부족하면 전부 거부하므로, 가능한 만큼으로 줄여 0에서 멈추게 한다(기획 51번 §4).
        // 늘리기는 지갑에 획득 상한이 없으므로(설계 83번 §3.1) 그대로 호출한다.
        private void Decrease(int step)
        {
            var amount = Mathf.Min(step, wallet.CurrentAmount);
            if (amount > 0) wallet.TrySpend(amount);
        }

        private static Rect Cell(Rect row, float cellWidth, int index)
        {
            return new Rect(row.x + cellWidth * index + CellPadding, row.y + CellPadding, cellWidth - CellPadding * 2f, row.height - CellPadding * 2f);
        }

        // GUI.skin은 OnGUI 안에서만 접근할 수 있어 첫 호출 때 만든다. 기본 스킨 글자가 작아 크기만 키운다.
        private void EnsureStyles()
        {
            if (buttonStyle != null) return;

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = FontSize };
            amountStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, alignment = TextAnchor.MiddleCenter };
            amountStyle.normal.textColor = Color.white;
            rowBackground = new Texture2D(1, 1);
            rowBackground.SetPixel(0, 0, Color.black);
            rowBackground.Apply();
            rowStyle = new GUIStyle(GUI.skin.box);
            rowStyle.normal.background = rowBackground;
        }
    }
}
#endif
