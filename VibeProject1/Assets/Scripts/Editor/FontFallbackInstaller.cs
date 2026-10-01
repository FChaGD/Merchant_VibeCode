using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 한글 SDF(정적 아틀라스)에 없는 기호가 □로 깨지지 않도록 기호용 동적 대체 폰트를 붙인다(Docs/설계/70번).
    /// 정적 자산에는 런타임 글자 추가가 불가능하고, 한글 SDF 자체를 동적으로 돌리면 남은 아틀라스(약 10%)가 차는 순간 다시 깨진다 -
    /// 그래서 같은 원본 TTF로 만든 별도 동적 자산을 대체 폰트 목록 맨 앞에 둔다(글씨체 동일).
    /// 한글 TTF에도 없는 문자(— 등)는 TMP Settings 전역 대체 폰트인 LiberationSans가 마지막으로 받는다.
    /// 폰트 자산과 TMP Settings만 바꾸므로 씬 저장은 필요 없다. 재실행해도 안전하다(get-or-create).
    /// </summary>
    internal static class FontFallbackInstaller
    {
        private const string FontAssetFolder = "Assets/Resources/Font/FontAssets";
        private const string FallbackSuffix = " Symbol Fallback";
        private const string LiberationSansPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const int FallbackAtlasSize = 1024;

        private static readonly string[] KoreanFontAssetPaths =
        {
            FontAssetFolder + "/Youth SDF.asset",
            FontAssetFolder + "/KCC-Ahnjunggeun SDF.asset",
        };

        [MenuItem("Tools/Game/Fonts/Install Symbol Fallback")]
        public static void Install()
        {
            foreach (var path in KoreanFontAssetPaths)
                InstallFor(path);

            InstallGlobalFallback();
            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(FontFallbackInstaller)}: 기호 대체 폰트 설치 완료.");
        }

        private static void InstallFor(string sourcePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"{nameof(FontFallbackInstaller)}: '{sourcePath}' 폰트 자산이 없어 건너뛴다.");
                return;
            }

            // 정적 자산은 런타임 원본 폰트 참조를 비워두므로 생성 설정에 남은 GUID로 TTF를 찾는다.
            var fontPath = AssetDatabase.GUIDToAssetPath(source.creationSettings.sourceFontFileGUID);
            var font = string.IsNullOrEmpty(fontPath) ? null : AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogWarning($"{nameof(FontFallbackInstaller)}: '{source.name}'의 원본 TTF를 찾지 못해 건너뛴다.");
                return;
            }

            var fallback = GetOrCreateFallback(source, font);
            PrefillCharacters(fallback);

            var table = source.fallbackFontAssetTable;
            if (!table.Contains(fallback))
            {
                table.Insert(0, fallback);
                EditorUtility.SetDirty(source);
            }
        }

        private static TMP_FontAsset GetOrCreateFallback(TMP_FontAsset source, Font font)
        {
            var path = $"{FontAssetFolder}/{source.name}{FallbackSuffix}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            // 샘플링 크기·패딩·렌더 모드를 원본과 맞춰야 한글과 섞였을 때 글자 크기·굵기가 같아 보인다.
            var fallback = TMP_FontAsset.CreateFontAsset(
                font,
                Mathf.RoundToInt(source.faceInfo.pointSize),
                source.atlasPadding,
                source.atlasRenderMode,
                FallbackAtlasSize,
                FallbackAtlasSize,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
            fallback.name = source.name + FallbackSuffix;

            AssetDatabase.CreateAsset(fallback, path);
            // 설계 70번 §7-3: 에디터에서 채운 아틀라스를 그대로 커밋·빌드에 싣는다. 속성이 internal이라 직렬화 필드로 지정한다.
            var so = new SerializedObject(fallback);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var atlas = fallback.atlasTextures[0];
            atlas.name = fallback.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fallback);
            fallback.material.name = atlas.name + " Material";
            AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            return fallback;
        }

        // 원본 아틀라스에서 빠진 Latin-1 보충(U+00A0–U+00FF, · × ° ± 등)을 미리 채운다 - 자주 쓰이는 기호가 Play 중 추가되며 생기는 자산 변경을 줄인다.
        private static void PrefillCharacters(TMP_FontAsset fallback)
        {
            var sb = new StringBuilder();
            for (var c = 0x00A0; c <= 0x00FF; c++)
                sb.Append((char)c);

            fallback.TryAddCharacters(sb.ToString(), out _);
            EditorUtility.SetDirty(fallback);
        }

        private static void InstallGlobalFallback()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansPath);
            if (liberation == null)
            {
                Debug.LogWarning($"{nameof(FontFallbackInstaller)}: '{LiberationSansPath}'가 없어 전역 대체 폰트 등록을 건너뛴다.");
                return;
            }

            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning($"{nameof(FontFallbackInstaller)}: TMP Settings를 찾지 못해 전역 대체 폰트 등록을 건너뛴다.");
                return;
            }

            var list = TMP_Settings.fallbackFontAssets ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (list.Contains(liberation)) return;

            list.Add(liberation);
            EditorUtility.SetDirty(settings);
        }
    }
}
