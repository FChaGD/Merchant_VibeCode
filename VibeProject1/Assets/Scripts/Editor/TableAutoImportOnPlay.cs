using UnityEditor;

namespace Game.Core.Editor
{
    /// <summary>
    /// 플레이 모드 진입 직전(에디트 모드를 벗어나는 시점)에 테이블 임포터 4종을 자동 실행해, 엑셀
    /// 최신 내용이 항상 반영된 상태로 플레이가 시작되게 한다(Docs/설계/17번 §9 후속, 18번 §9 - v2에서
    /// 워크북이 도메인별 4개로 분리되며 임포터도 4개로 늘었다). Import()는 AssetDatabase/
    /// SerializedObject를 쓰는 에디터 전용 API라 플레이 모드 진입 후에는 호출할 수 없어, ExitingEditMode
    /// 시점에 실행해야 한다. 워크북이 없거나 형식이 안 맞아도 각 임포터가 자체적으로 로그만 남기고
    /// 조용히 중단하므로(한 워크북의 오류가 다른 도메인 임포트까지 막지 않는다), 플레이 진입 자체를
    /// 막지는 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class TableAutoImportOnPlay
    {
        static TableAutoImportOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;

            CharacterStatsTableImporter.Import();
            EnemyStatsTableImporter.Import();
            PartyPolicyTableImporter.Import();
            RoleGroupTacticsTableImporter.Import();
        }
    }
}
