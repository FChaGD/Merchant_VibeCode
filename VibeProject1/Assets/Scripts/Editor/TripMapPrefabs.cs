using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 상행 준비 지도의 마커·도로 선 프리팹을 get-or-create로 만든다(Docs/설계/69번 §7). Hub 인스톨러(지도 화면)와 Bootstrap
    /// 인스톨러(TripPanel 필드 배선)가 함께 쓰므로 어느 한 인스톨러 안에 두지 않는다(CLAUDE.md - 인스톨러끼리 내부 메서드 의존 금지).
    /// 예전엔 Hub 인스톨러가 프리팹만 만들고 TripPanel 연결은 수동이었다 - 이제 Bootstrap 인스톨러가 여기서 받아 바로 연결한다.
    /// 도시·관문은 같은 컴포넌트(TripMapMarkerView)를 쓰고 모양·라벨만 다르다. 파일 이름은 기존 프리팹을 그대로 이어 쓰려고 유지한다.
    /// </summary>
    internal static class TripMapPrefabs
    {
        private const string Folder = "Assets/Prefabs/UI/Trip";
        private const string CityMarkerPath = Folder + "/TripDebugCityMarker.prefab";
        private const string GateMarkerPath = Folder + "/TripGateMarker.prefab";
        private const string RoadLinePath = Folder + "/TripDebugRoadLine.prefab";

        private static readonly Color GateColor = new(0.55f, 0.35f, 0.75f, 1f);

        public static TripMapMarkerView GetOrCreateCityMarker()
        {
            EnsureFolder();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CityMarkerPath);
            if (existing != null) return existing.GetComponent<TripMapMarkerView>();

            var go = CreateMarkerRoot("TripCityMarker", new Vector2(48f, 48f), FormationPlaceholderIcons.GetOrCreateCircle(), Color.white, out var image);
            var view = go.AddComponent<TripMapMarkerView>();
            var so = new SerializedObject(view);
            so.FindProperty("iconImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();
            return Save(go, CityMarkerPath);
        }

        // 관문은 도시와 다른 모양(사각형) + 가리키는 지역 이름 라벨(기획 68번 §4-8).
        public static TripMapMarkerView GetOrCreateGateMarker()
        {
            EnsureFolder();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GateMarkerPath);
            if (existing != null) return existing.GetComponent<TripMapMarkerView>();

            var go = CreateMarkerRoot("TripGateMarker", new Vector2(44f, 44f), FormationPlaceholderIcons.GetOrCreateSquare(), GateColor, out var image);
            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(go.transform, false);
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(160f, 28f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            var label = EditorUIBuilder.EnsureText(labelGo);
            label.fontSize = 18f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Top;
            label.raycastTarget = false;

            var view = go.AddComponent<TripMapMarkerView>();
            var so = new SerializedObject(view);
            so.FindProperty("iconImage").objectReferenceValue = image;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedProperties();
            return Save(go, GateMarkerPath);
        }

        public static TripRoadLineView GetOrCreateRoadLine()
        {
            EnsureFolder();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RoadLinePath);
            if (existing != null)
            {
                // 예전에 만든 프리팹에는 lineImage 연결이 빠져 있을 수 있다 - 재실행 때 맞춘다.
                var existingView = existing.GetComponent<TripRoadLineView>();
                var existingSo = new SerializedObject(existingView);
                existingSo.FindProperty("lineImage").objectReferenceValue = existing.GetComponent<Image>();
                existingSo.ApplyModifiedProperties();
                return existingView;
            }

            var go = new GameObject("TripRoadLine", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 6f);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            image.raycastTarget = true;

            var view = go.AddComponent<TripRoadLineView>();
            var so = new SerializedObject(view);
            so.FindProperty("lineImage").objectReferenceValue = image;
            so.ApplyModifiedProperties();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, RoadLinePath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<TripRoadLineView>();
        }

        private static GameObject CreateMarkerRoot(string name, Vector2 size, Sprite sprite, Color color, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = true;
            return go;
        }

        private static TripMapMarkerView Save(GameObject go, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<TripMapMarkerView>();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Trip");
        }
    }
}
