#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 재화 패널 바로 아래에 "[-1000] [-100] 현재 금액 [+100] [+1000]" 한 줄을 그린다(OnGUI).
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
        // 재화 패널 바로 아래 띠로 뜨는 툴팁(설계 83번 §6.1, 간격 4 + 높이 36)을 피한다 - OnGUI는 uGUI 위에 그려져 겹치면 툴팁이 가려진다.
        private const float TooltipBand = 40f;
        private const float RowGap = 8f;
        private const float RowHeight = 32f;
        private const float CellPadding = 2f;
        private const int FontSize = 16;
        private const int SmallStep = 100;
        private const int LargeStep = 1000;

        private IPlayerCurrencyWallet wallet;
        private bool isHubLoaded;
        private GUIStyle buttonStyle;
        private GUIStyle amountStyle;

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
            if (scene.name == SceneNames.Hub) isHubLoaded = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name == SceneNames.Hub) isHubLoaded = false;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || wallet == null || !isHubLoaded) return;
            EnsureStyles();

            var width = Screen.width * HudWidthRatio;
            var row = new Rect(Screen.width - HudMarginRight - width, HudMarginTop + HudEstimatedHeight + TooltipBand + RowGap, width, RowHeight);
            GUI.Box(row, GUIContent.none);

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
        }
    }
}
#endif
