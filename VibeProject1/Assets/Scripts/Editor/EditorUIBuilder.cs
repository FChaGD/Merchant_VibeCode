using System.Collections.Generic;
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

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
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

        /// <summary>
        /// 배경(Image)+체크마크(Image)를 갖춘 최소 토글. 프로젝트 최초 도입(방향성 지시 UI,
        /// Docs/설계/12번 §5.6-1) - 그 전까지는 버튼 기반 UI만 있었다.
        /// </summary>
        public static Toggle EnsureToggle(GameObject go)
        {
            EnsureImage(go, new Color(0.9f, 0.9f, 0.9f, 1f));
            var toggle = GetOrAddComponent<Toggle>(go);

            var checkGo = GetOrCreateUIObject(go.transform, "Checkmark");
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(16f, 16f);
            checkRect.anchoredPosition = Vector2.zero;
            var checkImage = EnsureImage(checkGo, new Color(0.2f, 0.6f, 0.2f, 1f));

            toggle.targetGraphic = go.GetComponent<Image>();
            toggle.graphic = checkImage;
            return toggle;
        }

        private static TMP_DefaultControls.Resources s_dropdownResources;

        private static TMP_DefaultControls.Resources GetDropdownResources()
        {
            if (s_dropdownResources.standard == null)
            {
                s_dropdownResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                s_dropdownResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                s_dropdownResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
                s_dropdownResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                s_dropdownResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
                s_dropdownResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
                s_dropdownResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            }
            return s_dropdownResources;
        }

        /// <summary>
        /// 손으로 계층을 조립한 버전은 항목(Item) 텍스트가 팝업에서 안 보이는 원인 불명 버그가 있었다
        /// (RectTransform/알파/폰트 전부 정상인데 렌더링만 안 됨 - 유니티 기본 드롭다운은 대조 테스트로
        /// 정상 확인됨). 그래서 직접 조립하지 않고, 유니티 "Dropdown - TextMeshPro" 메뉴가 실제로 쓰는
        /// TMP_DefaultControls.CreateDropdown 결과물을 그대로 가져와 go 밑으로 옮겨 붙인다.
        /// </summary>
        public static TMP_Dropdown EnsureDropdown(GameObject go)
        {
            var existingDropdown = go.GetComponent<TMP_Dropdown>();
            if (existingDropdown != null && go.transform.Find("Template") != null)
            {
                return existingDropdown;
            }

            DestroyChildIfExists(go.transform, "Label");
            DestroyChildIfExists(go.transform, "Arrow");
            DestroyChildIfExists(go.transform, "Template");
            if (existingDropdown != null)
            {
                Undo.DestroyObjectImmediate(existingDropdown);
            }

            var reference = TMP_DefaultControls.CreateDropdown(GetDropdownResources());
            reference.transform.SetParent(go.transform, false);

            var children = new List<Transform>();
            foreach (Transform child in reference.transform)
            {
                children.Add(child);
            }
            foreach (var child in children)
            {
                child.SetParent(go.transform, false);
                SetLayerRecursively(child.gameObject, go.layer);
            }

            var referenceImage = reference.GetComponent<Image>();
            var image = EnsureImage(go, referenceImage.color);
            image.sprite = referenceImage.sprite;
            image.type = referenceImage.type;

            var referenceDropdown = reference.GetComponent<TMP_Dropdown>();
            var dropdown = GetOrAddComponent<TMP_Dropdown>(go);
            dropdown.targetGraphic = image;
            dropdown.colors = referenceDropdown.colors;
            dropdown.template = go.transform.Find("Template").GetComponent<RectTransform>();
            dropdown.captionText = go.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            dropdown.itemText = go.transform.Find("Template/Viewport/Content/Item/Item Label").GetComponent<TextMeshProUGUI>();
            dropdown.options.Clear();

            // 고정 폰트 크기(과거 16/15)는 UI 스케일이 바뀔 때마다 값을 다시 잡아줘야 해서, 라벨/항목
            // 텍스트칸 크기에 맞춰 자동으로 커지는 Auto Size(TMP enableAutoSizing)로 바꿨다.
            dropdown.captionText.enableAutoSizing = true;
            dropdown.captionText.fontSizeMin = DropdownFontSizeMin;
            dropdown.captionText.fontSizeMax = DropdownFontSizeMax;
            dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText.color = Color.black;
            dropdown.itemText.enableAutoSizing = true;
            dropdown.itemText.fontSizeMin = DropdownFontSizeMin;
            dropdown.itemText.fontSizeMax = DropdownFontSizeMax;
            dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.itemText.color = Color.black;

            // 팝업 항목 한 줄의 높이를 TMP_DefaultControls 기본값(20)이 아니라 드롭다운 UI 자체의
            // 높이만큼 키운다 - Auto Size는 텍스트칸이 클수록 글자도 커지므로, 항목 칸을 키워야
            // 항목 텍스트도 라벨만큼 커진다. 드롭다운 RectTransform은 앵커 스트레치라 이 시점엔 이미
            // 실제 높이가 계산되어 있다(레이아웃 그룹이 없어 별도 리빌드가 필요 없음).
            // Content의 높이도 Item과 함께 맞춰야 한다 - TMP_Dropdown.Show()가 "Item rect와 Content
            // rect의 차이"를 항목-배경 사이 여백으로 계산해 매 항목에 적용하는데(offsetMin/offsetMax),
            // Item만 키우고 Content(기본 28)를 그대로 두면 이 여백 계산이 깨져 팝업이 드롭다운 UI 중심과
            // 겹치는 위치로 밀리고 첫 항목이 잘려 보인다 - 실플레이에서 확인된 버그.
            var dropdownHeight = go.GetComponent<RectTransform>().rect.height;
            if (dropdownHeight > 0f)
            {
                var itemRect = go.transform.Find("Template/Viewport/Content/Item").GetComponent<RectTransform>();
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, dropdownHeight);
                var contentRect = go.transform.Find("Template/Viewport/Content").GetComponent<RectTransform>();
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, dropdownHeight);
            }

            // 사이드 스크롤바 제거(사용자 지시) - 세로 드래그/휠 스크롤은 ScrollRect 자체 기능이라
            // 스크롤바를 없애도 항목이 뷰포트보다 많아지면 여전히 스크롤할 수 있다. 스크롤바가 차지하던
            // 폭(18)을 Viewport에 되돌려줘 항목이 전체 폭을 쓴다.
            var templateTransform = go.transform.Find("Template");
            var scrollbarTransform = templateTransform.Find("Scrollbar");
            if (scrollbarTransform != null)
            {
                Undo.DestroyObjectImmediate(scrollbarTransform.gameObject);
            }
            templateTransform.GetComponent<ScrollRect>().verticalScrollbar = null;
            var viewportRect = templateTransform.Find("Viewport").GetComponent<RectTransform>();
            viewportRect.sizeDelta = new Vector2(0f, viewportRect.sizeDelta.y);

            // 팝업이 알파 0→1로 페이드인되는 도중 스크린샷/빠른 확인 시 "안 보인다"로 오인되는 걸
            // 막기 위해 즉시 표시되게 한다.
            dropdown.alphaFadeSpeed = 0f;

            Undo.DestroyObjectImmediate(reference);

            return dropdown;
        }

        private const float DropdownFontSizeMin = 8f;
        private const float DropdownFontSizeMax = 32f;

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
