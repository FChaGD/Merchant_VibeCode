using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub/Bootstrap 씬에 UI 하이어라키를 코드로 생성하는 여러 Editor 인스톨러(HubSceneInstaller,
    /// FieldUIInstaller 등)가 공유하는 범용 씬 조립 도구. 특정 UI 기능에 대한 지식은 갖지 않고,
    /// "오브젝트를 만들거나 재사용", "앵커 설정", "컴포넌트 부착" 같은 저수준 동작만 제공한다.
    /// 각 인스톨러가 서로의 구현 세부사항에 의존하지 않도록, 이런 공용 동작은 이 클래스에만 둔다.
    /// </summary>
    internal static class EditorUIBuilder
    {
        public static GameObject GetOrCreateUIObject(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            return go;
        }

        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// 이름/구조가 바뀌어 더 이상 쓰이지 않는 옛 자식 오브젝트를 정리한다. 인스톨러를 재실행해도
        /// 안전하려면(get-or-create만으로는 이전 버전의 잔재가 남을 수 있어) 이 헬퍼로 명시적으로 치운다.
        /// </summary>
        public static void DestroyChildIfExists(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        public static void SetStretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        public static Image EnsureImage(GameObject go, Color color)
        {
            var image = GetOrAddComponent<Image>(go);
            image.color = color;
            return image;
        }

        public static Button EnsureButton(GameObject go)
        {
            var button = GetOrAddComponent<Button>(go);
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        public static TMP_Text EnsureLabel(Transform parent, string text)
        {
            var labelGo = GetOrCreateUIObject(parent, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            var label = GetOrAddComponent<TextMeshProUGUI>(labelGo);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24;
            label.color = Color.black;
            label.raycastTarget = false;
            return label;
        }

        public static void EnsureMarker(GameObject go, string id)
        {
            var marker = GetOrAddComponent<UIElementMarker>(go);
            var so = new SerializedObject(marker);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// 스크롤 영역이 공유하는 Viewport(+RectMask2D)/Content 뼈대를 만든다.
        /// 앵커·레이아웃 그룹·ContentSizeFitter는 호출자가 용도에 맞게 이어서 구성한다.
        /// </summary>
        public static (RectTransform viewport, GameObject content) CreateViewportAndContent(Transform root)
        {
            var viewportGo = GetOrCreateUIObject(root, "Viewport");
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            SetStretch(viewportRect);
            EnsureImage(viewportGo, new Color(1f, 1f, 1f, 0.001f));
            GetOrAddComponent<RectMask2D>(viewportGo);

            var contentGo = GetOrCreateUIObject(viewportRect, "Content");
            return (viewportRect, contentGo);
        }

        public static void ConfigureScrollRect(GameObject go, RectTransform viewport, RectTransform content, bool horizontal, bool vertical)
        {
            var scrollRect = GetOrAddComponent<ScrollRect>(go);
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        /// <summary>
        /// 콘텐츠 씬(Hub/Field)마다 자체 EventSystem을 가져야 한다 - 그리드 ScrollRect 드래그 등 UI
        /// 입력이 정상 동작하려면 씬이 로드될 때마다 새 EventSystem이 함께 있어야 한다는 게 확인됐다
        /// (영속 EventSystem 하나로 통합했더니 드래그가 깨졌다). 대신 SceneLoader가 씬 전환 시 새 씬을
        /// 로드하기 전에 이전 EventSystem부터 파괴해 두 EventSystem이 동시에 존재하는 프레임 자체를
        /// 없앤다(SceneLoader.cs 참고). 각 콘텐츠 씬 인스톨러는 이 헬퍼로 자기 씬에 EventSystem이
        /// 있는지 확인하고 없으면 만든다.
        /// </summary>
        public static void EnsureSceneEventSystem(Scene scene)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.GetComponent<EventSystem>() != null)
                {
                    return;
                }
            }

            var go = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private const string SolidSpritePath = "Assets/Sprites/UI/SolidWhite.png";

        /// <summary>
        /// Image.Type.Filled(게이지/진행바 등)는 sprite가 비어 있으면 fillAmount를 무시하고 항상 꽉 찬
        /// 채로 그려지는 경우가 있다 - 이 흰색 단색 스프라이트를 붙이면 정상적으로 채워진다.
        /// Image.color로 원하는 색을 입히면 되므로 색상별로 별도 스프라이트를 만들 필요는 없다.
        /// </summary>
        public static Sprite GetOrCreateSolidSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            {
                AssetDatabase.CreateFolder("Assets", "Sprites");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Sprites/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Sprites", "UI");
            }

            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(SolidSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SolidSpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SolidSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SolidSpritePath);
        }
    }
}
