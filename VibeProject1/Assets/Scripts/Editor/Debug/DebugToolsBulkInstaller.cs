using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor.DebugTools
{
    /// <summary>
    /// Tools/Game/Debug/Install/·Remove/ 아래 등록된 토글형 디버그 도구(설치/제거가 짝을 이루는 것)를
    /// 한 번에 켜고 끈다. 씬을 새로 받았거나 오래 쉬었다 다시 작업을 시작할 때, 도구를 하나씩 찾아
    /// 실행하는 대신 이 메뉴 하나로 전부 동기화하기 위한 용도 - 개별 Install/Remove 메뉴는 각자 특정
    /// 도구만 다시 설치하고 싶을 때 그대로 쓴다. 새 토글형 디버그 도구를 추가하면 이 파일에도 한 줄씩
    /// 추가할 것(반사(reflection)로 자동 수집하지 않는 이유는 항목 수가 적어 명시적 나열이 더
    /// 읽기 쉽고, 실행 순서를 이 파일에서 명확히 통제할 수 있어서다).
    /// "Build Battle Test Scene"(BattleTestSceneInstaller)은 상태를 켜고 끄는 토글이 아니라 씬을
    /// 새로 만드는 1회성 작업이라 여기 포함하지 않는다.
    /// </summary>
    public static class DebugToolsBulkInstaller
    {
        [MenuItem("Tools/Game/Debug/Install All")]
        public static void InstallAll()
        {
            DebugBootstrapReentryGuardInstaller.InstallGuards();
            BattleGizmoInstaller.InstallGizmos();
            BattleDebugResultInstaller.InstallResultButtons();
            PlayerCurrencyDebugInstaller.InstallSpinButtons();
            FormationDebugPinInstaller.InstallPins();
            FormationAreaOutlineDebugInstaller.InstallOutline();
            TripDebugInstaller.InstallTripDebugButtons();

            Debug.Log("디버그 도구 전체 설치/동기화 완료.");
        }

        [MenuItem("Tools/Game/Debug/Remove All")]
        public static void RemoveAll()
        {
            DebugBootstrapReentryGuardInstaller.RemoveGuards();
            BattleGizmoInstaller.RemoveGizmos();
            BattleDebugResultInstaller.RemoveResultButtons();
            PlayerCurrencyDebugInstaller.RemoveSpinButtons();
            FormationDebugPinInstaller.RemovePins();
            FormationAreaOutlineDebugInstaller.RemoveOutline();
            TripDebugInstaller.RemoveTripDebugButtons();

            Debug.Log("디버그 도구 전체 제거 완료.");
        }
    }
}
