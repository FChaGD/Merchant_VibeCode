using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 엑셀 시트 하나를 "헤더 행 기준 컬럼명→셀 문자열" 로우 목록으로 변환하는 것까지만 책임진다 -
    /// 타입 변환(enum/숫자 파싱)과 도메인 검증은 각 도메인 임포터(CharacterStatsTableImporter,
    /// EnemyStatsTableImporter, PartyPolicyTableImporter, RoleGroupTacticsTableImporter)의 몫이다.
    /// EditorUIBuilder와 같은 성격의 공용 저수준 유틸리티(Docs/설계/17번 §2, 18번) - 여러 임포터가
    /// 이 유틸리티에 의존하되 서로의 내부를 열어 쓰지 않는다.
    /// </summary>
    internal static class EditorTableReader
    {
        private static bool codePagesRegistered;

        public static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadSheet(string workbookPath, string sheetName)
        {
            // ExcelReaderConfiguration의 기본 생성자가 xlsx/xls 여부와 무관하게 항상 코드페이지 1252로
            // FallbackEncoding을 계산하는데, .NET(코어) 런타임은 이 코드페이지를 기본 내장하지 않아
            // System.Text.Encoding.CodePages 프로바이더를 등록해두지 않으면 NotSupportedException이
            // 난다(실제 워크북으로 검증하며 발견 - Assets/Plugins/Editor/ExcelDataReader/ 참고).
            if (!codePagesRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                codePagesRegistered = true;
            }

            using var stream = File.Open(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            do
            {
                if (!string.Equals(reader.Name, sheetName, StringComparison.Ordinal)) continue;
                return ReadCurrentSheet(reader);
            } while (reader.NextResult());

            throw new InvalidOperationException($"'{workbookPath}'에 시트 '{sheetName}'가 없다.");
        }

        // 헤더 행(첫 행)을 컬럼명으로 삼고, 그 뒤 행들을 컬럼명→셀 문자열 딕셔너리로 변환한다.
        // 모든 셀이 빈 값인 행(엑셀에서 흔한 트레일링 빈 행)은 건너뛴다.
        private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCurrentSheet(IExcelDataReader reader)
        {
            if (!reader.Read())
            {
                return Array.Empty<IReadOnlyDictionary<string, string>>();
            }

            var headers = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                headers[i] = reader.GetValue(i)?.ToString() ?? string.Empty;
            }

            var rows = new List<IReadOnlyDictionary<string, string>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, string>(headers.Length);
                var isEmpty = true;
                for (var i = 0; i < reader.FieldCount && i < headers.Length; i++)
                {
                    var value = reader.GetValue(i)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value)) isEmpty = false;
                    row[headers[i]] = value;
                }

                if (!isEmpty) rows.Add(row);
            }

            return rows;
        }

        // 셀 문자열 → 타입 변환 헬퍼. 4개 임포터가 공통으로 쓰는 파싱 규칙이라 "시트 읽기"와 같은
        // 자리에 둔다 - enum/숫자 컬럼 파싱 실패는 임포트 시점에 곧장 예외로 드러나야 한다(값이
        // 조용히 0/default로 새는 것을 방지).
        public static float ParseFloat(IReadOnlyDictionary<string, string> row, string column)
        {
            if (!float.TryParse(row[column], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"컬럼 '{column}' 값 '{row[column]}'을(를) float로 해석할 수 없다.");
            }
            return value;
        }

        public static int ParseInt(IReadOnlyDictionary<string, string> row, string column)
        {
            if (!int.TryParse(row[column], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"컬럼 '{column}' 값 '{row[column]}'을(를) int로 해석할 수 없다.");
            }
            return value;
        }

        // v2부터 셀 값은 enum 이름 문자열이 아니라 정수 Id다(Docs/설계/18번 §3/§4) - enum 멤버에
        // 명시적으로 박아둔 정수값이 곧 Id. Enum.IsDefined로 정의되지 않은 Id를 즉시 예외로 드러낸다.
        public static TEnum ParseEnum<TEnum>(IReadOnlyDictionary<string, string> row, string column) where TEnum : struct, Enum
        {
            var id = ParseInt(row, column);
            if (!Enum.IsDefined(typeof(TEnum), id))
            {
                throw new FormatException($"컬럼 '{column}' 값 '{id}'은(는) {typeof(TEnum).Name}에 정의되지 않은 Id다.");
            }
            return (TEnum)Enum.ToObject(typeof(TEnum), id);
        }

        public static bool ParseBool(IReadOnlyDictionary<string, string> row, string column)
        {
            if (!bool.TryParse(row[column], out var value))
            {
                throw new FormatException($"컬럼 '{column}' 값 '{row[column]}'을(를) bool로 해석할 수 없다(TRUE/FALSE만 허용).");
            }
            return value;
        }

        // SerializedProperty.enumValueIndex는 enum의 "선언 순서(ordinal)"를 받지, 멤버에 박아둔
        // 실제 정수값을 받지 않는다 - v1은 선언 순서와 정수값이 우연히 같아 드러나지 않았지만, v2가
        // 9개 enum에 1부터 시작하는 명시적 Id를 부여하면서 어긋난다(Docs/설계/18번 §2). intValue는
        // enum의 실제 정수값을 그대로 읽고 쓴다 - 모든 임포터가 enum 필드를 쓸 때 반드시 이 메서드를
        // 거치게 해 enumValueIndex 오용 회귀를 원천 차단한다.
        public static void SetEnumValue<TEnum>(SerializedProperty property, TEnum value) where TEnum : struct, Enum
        {
            property.intValue = Convert.ToInt32(value);
        }

        // SO 에셋 get-or-create - 여러 임포터가 필요로 해(Docs/설계/17번 §10.4) 공용 유틸리티로
        // 승격했다. 씬 인스톨러의 GetOrCreateManager와 같은 성격(재실행해도 안전 - 있으면 재사용,
        // 없으면 생성).
        public static T GetOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }
    }
}
