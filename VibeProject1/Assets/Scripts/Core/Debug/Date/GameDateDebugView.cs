#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - Hub 날짜 HUD 오른쪽에 [+1] [+10] [+30](일)을 그린다(OnGUI, Docs/설계/85번 §5). 월 넘김(+30)·해 넘김(반복)을
    /// 상행 없이 확인하기 위한 도구. DateManager와 같은 GameObject(Bootstrap 상주)에 붙고 Hub가 로드된 동안에만 그린다.
    /// 날짜는 진행 계약으로만 바꾼다 - 날짜 매니저에 디버그 전용 기능을 추가하지 않아야 이 파일을 지우는 것만으로 걷어낼 수 있다.
    /// 걷어낼 때는 Remove 메뉴를 먼저 실행한 뒤 이 파일·GameDateDebugInstaller.cs(+.meta)를 지우고 DebugToolsBulkInstaller의 두 줄을 지운다.
    /// </summary>
    public class GameDateDebugView : MonoBehaviour
    {
        private static readonly int[] Steps = { 1, 10, 30 };
        private static readonly string[] Labels = { "+1", "+10", "+30" }; // OnGUI 호출마다 문자열을 만들지 않게 미리 둔다
        // HubSceneInstaller.BuildGameDateHud 배치(상단 중앙, 위 여백 32, 높이 40)와 맞춘 값. 게임 코드와 상수를 공유하지 않는 이유는
        // 이 디버그 코드를 걷어낼 때 게임 코드를 고치지 않기 위해서다 - 어긋나도 버튼 위치만 틀어진다. Hub 캔버스가 ConstantPixelSize라
        // OnGUI 픽셀 좌표와 단위가 같다.
        private const float DatePanelTop = 32f;
        private const float DatePanelWidth = 200f;
        private const float DatePanelHeight = 40f;
        // 날짜 패널 오른쪽 같은 줄에 둔다 - 패널 아래 줄은 720p에서 시설 화면 헤더·전술 카드와 겹쳤다(2026-10-11 실측). 오른쪽은
        // 재화 HUD(화면 우측 30%)까지 1280x720 기준 약 116px뿐이라 버튼을 좁히고 문구를 "+N"으로 줄였다.
        private const float RowGap = 6f;
        private const float ButtonWidth = 36f;
        private const float ButtonHeight = 28f;
        private const float Gap = 2f;

        private IDateAdvancer dateAdvancer;
        private bool isHubLoaded;

        private void Awake()
        {
            dateAdvancer = GetComponent<IDateAdvancer>();
            if (dateAdvancer == null)
            {
                Debug.LogWarning($"{nameof(GameDateDebugView)}: 같은 오브젝트에서 날짜 매니저를 찾지 못해 버튼을 그리지 않는다.", this);
            }
        }

        // OnGUI는 프레임당 여러 번 불리므로 매번 씬을 조회하지 않고 로드/언로드 이벤트로 갱신한다.
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
            if (!Application.isPlaying || dateAdvancer == null || !isHubLoaded) return;

            var x = (Screen.width + DatePanelWidth) / 2f + RowGap;
            var y = DatePanelTop + (DatePanelHeight - ButtonHeight) / 2f;
            for (var i = 0; i < Steps.Length; i++)
            {
                if (GUI.Button(new Rect(x + i * (ButtonWidth + Gap), y, ButtonWidth, ButtonHeight), Labels[i])) dateAdvancer.AdvanceDays(Steps[i]);
            }
        }
    }
}
#endif
