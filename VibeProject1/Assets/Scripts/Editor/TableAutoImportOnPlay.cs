using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 플레이 모드 진입 직전(에디트 모드를 벗어나는 시점)에 테이블 임포터를 자동 실행해, 엑셀
    /// 최신 내용이 항상 반영된 상태로 플레이가 시작되게 한다(Docs/설계/17번 §9 후속, 18번 §9 - v2에서
    /// 워크북이 도메인별 4개로 분리되며 임포터도 4개로 늘었다, 20번 §6 - 도시 지도용 5번째 추가,
    /// 35번 §5 - 아이템 카테고리 4종 추가, 50번 §4 - 아이템은 공용 워크북과 조인하는 일괄 임포트 1개로 통합).
    /// Import()는 AssetDatabase/SerializedObject를 쓰는 에디터 전용 API라 플레이 모드 진입 후에는
    /// 호출할 수 없어, ExitingEditMode 시점에 실행해야 한다. 워크북이 없거나 형식이 안 맞아도 각
    /// 임포터가 자체적으로 로그만 남기고 조용히 중단하므로(한 워크북의 오류가 다른 도메인 임포트까지
    /// 막지 않는다), 플레이 진입 자체를 막지는 않는다.
    /// 임포트는 Play 진입 직전에 돌기 때문에 Console의 Clear on Play가 켜져 있으면 여기서 찍힌 경고·오류가
    /// 곧바로 지워진다(2026-10-13 확인). 그래서 임포트 중 경고·오류를 모아 두었다가 Play 진입 후 다시 출력한다.
    /// 진입 시 도메인 리로드로 정적 상태가 사라지므로 SessionState에 보관한다.
    /// </summary>
    [InitializeOnLoad]
    public static class TableAutoImportOnPlay
    {
        private const string PendingLogsKey = "Game.TableAutoImportOnPlay.PendingLogs";
        private const string ReplayPrefix = "[테이블 임포트 · Play 진입 후 재출력] ";

        [Serializable]
        private sealed class CapturedLogs
        {
            public List<string> messages = new List<string>();
            public List<bool> isError = new List<bool>();
        }

        static TableAutoImportOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ImportAllCapturingLogs();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Console 비우기가 진입 처리와 같은 프레임에 일어날 수 있어 한 틱 뒤에 출력한다.
                EditorApplication.delayCall += ReplayPendingLogs;
            }
        }

        private static void ImportAllCapturingLogs()
        {
            var captured = new CapturedLogs();
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Log) return;
                captured.messages.Add(condition);
                captured.isError.Add(type != LogType.Warning);
            }

            Application.logMessageReceived += Capture;
            try
            {
                CharacterStatsTableImporter.Import();
                EnemyStatsTableImporter.Import();
                PartyPolicyTableImporter.Import();
                RoleGroupTacticsTableImporter.Import();
                CityTableImporter.Import();
                ItemTableImporter.Import();
                CaravanAssetTableImporter.Import();
                // 재고 검증이 다른 테이블의 원본 엑셀을 직접 읽으므로 순서와 무관하지만, 참조 대상 뒤에 둔다(설계 81번 §3.2).
                TownStockTableImporter.Import();
                // 화자 검증이 캐릭터 원본 엑셀을 직접 읽으므로 순서와 무관하지만, 참조 대상 뒤에 둔다(설계 87번 §3.1).
                EncounterTableImporter.Import();
            }
            catch (Exception exception)
            {
                // 예외도 재출력 대상에 넣기 위해 구독 중에 기록한다. 이후 임포터를 건너뛰는 동작은 기존과 같다.
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            if (captured.messages.Count == 0)
            {
                SessionState.EraseString(PendingLogsKey);
                return;
            }
            SessionState.SetString(PendingLogsKey, JsonUtility.ToJson(captured));
        }

        private static void ReplayPendingLogs()
        {
            var json = SessionState.GetString(PendingLogsKey, string.Empty);
            SessionState.EraseString(PendingLogsKey);
            if (string.IsNullOrEmpty(json)) return;

            var captured = JsonUtility.FromJson<CapturedLogs>(json);
            if (captured == null) return;
            for (var i = 0; i < captured.messages.Count; i++)
            {
                var message = ReplayPrefix + captured.messages[i];
                if (captured.isError[i]) Debug.LogError(message);
                else Debug.LogWarning(message);
            }
        }
    }
}
