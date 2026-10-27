using System.Collections.Generic;
using System.IO;
using Game.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        /// <summary>
        /// UI가 아닌 월드 오브젝트(전장 라인렌더러/마커 등) 버전의 get-or-create. 이름이 같은 자식이
        /// 이미 여럿 있으면(과거 `new GameObject(...)`를 그대로 호출하던 코드가 재실행마다 중복
        /// 생성해 실제로 발생했던 문제 - BattleTestExtentGizmoView의 "ExtentBox"가 재실행 8회 만에
        /// 8개로 늘어나 있었다) 하나만 남기고 나머지를 정리해 자동으로 복구한다.
        /// </summary>
        public static GameObject GetOrCreateWorldChild(Transform parent, string name, int layer)
        {
            GameObject keep = null;
            var duplicates = new List<GameObject>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name != name) continue;
                if (keep == null) keep = child.gameObject;
                else duplicates.Add(child.gameObject);
            }
            foreach (var duplicate in duplicates) Undo.DestroyObjectImmediate(duplicate);

            if (keep != null) return keep;

            var go = new GameObject(name);
            go.layer = layer;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
            return go;
        }

        /// <summary>
        /// 씬 루트(최상위) 오브젝트를 이름으로 찾아 재사용하거나 없으면 새로 만든다 - Managers 루트처럼
        /// 씬마다 하나만 있어야 하는 최상위 컨테이너에 쓴다(ManagerHierarchyInstaller/
        /// BattleTestSceneInstaller 공용).
        /// </summary>
        public static GameObject GetOrCreateSceneRoot(Scene scene, string name)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == name)
                {
                    return rootObject;
                }
            }

            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            return root;
        }

        /// <summary>
        /// 매니저류 컴포넌트를 parent의 자식 오브젝트(이름=objectName)에 get-or-create로 부착한다
        /// (ManagerHierarchyInstaller/BattleTestSceneInstaller 공용 - 둘 다 "매니저 하이어라키를 코드로
        /// 재현 가능하게 만든다"는 같은 패턴을 쓴다).
        /// </summary>
        public static T GetOrCreateManager<T>(Transform parent, string objectName) where T : Component
        {
            var existing = parent.Find(objectName);
            if (existing != null)
            {
                var component = existing.GetComponent<T>();
                return component != null ? component : Undo.AddComponent<T>(existing.gameObject);
            }

            var go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
            Undo.SetTransformParent(go.transform, parent, $"Parent {objectName}");
            return Undo.AddComponent<T>(go);
        }

        /// <summary>
        /// 리팩토링 과정에서 컴포넌트 스크립트 파일 자체를 지운 적이 있다(예: BattleResultEvaluator) -
        /// 씬에 이미 저장돼 있던 해당 컴포넌트 참조는 삭제된 타입이라 GetComponent&lt;T&gt;()로 찾아
        /// 제거할 방법이 없다("Missing Script" 경고로 남는다). 재실행할 때마다 하이어라키 전체를 훑어
        /// 없어진 스크립트 참조를 걷어낸다.
        /// </summary>
        public static void RemoveMissingScriptsRecursively(Transform root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }
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

        /// <summary>
        /// 씬 전환 연출은 콘텐츠 씬이 등록한 전환 루트 하나만 밀어낸다 - 그 밖에 만든 UI는 전환 중 제자리에
        /// 남는다(Docs/설계/38번 §10). Canvas 직계 자식이 전환 루트뿐인지 검사해 새 UI를 루트 밖에 만드는
        /// 실수를 조기에 드러낸다. Hub/Field 인스톨러가 공유한다.
        /// </summary>
        public static void WarnIfOutsideTransitionRoot(Transform canvasRoot, Transform transitionRoot, string sceneLabel)
        {
            foreach (Transform child in canvasRoot)
            {
                if (child != transitionRoot)
                {
                    Debug.LogWarning($"{sceneLabel} Canvas 바로 아래에 전환 루트({transitionRoot.name}) 밖 UI '{child.name}'가 있다 - 씬 전환 시 화면과 함께 이동하지 않는다.", child);
                }
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

        // Hub 씬의 "배치" 버튼(HubUIElementIds.FormationButton)이 처음부터 갖고 있던 모양 - 색만 다른
        // 평면 사각형(EnsureImage)이 아니라 Unity 기본 UI 스프라이트(둥근 테두리, Sliced)를 쓴다.
        // 그 버튼은 코드로 만들어진 게 아니라 씬에 원래 있던 것이라 이 값을 코드로 옮길 곳이 없었는데,
        // Field의 같은 역할 버튼(정비창)을 여기 맞추기 위해(사용자 확정, 2026-09-07) 이 헬퍼로 뽑았다.
        public static Image EnsureStandardButtonImage(GameObject go)
        {
            var image = EnsureImage(go, Color.white);
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
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
            => EnsureLabel(parent, text, autoSize: false, minFontSize: 18f, maxFontSize: 24f);

        // autoSize=true면 텍스트가 상자 폭에 맞춰 minFontSize~maxFontSize 사이로 자동 축소된다 -
        // 상시 노출 액션 버튼(배치/정비창, 방향성 지시)처럼 좁은 버튼 안에 라벨이 잘리지 않아야 하는
        // 곳에 쓴다. 이 값들은 인스펙터에서 먼저 확정한 뒤(사용자 확정, 2026-09-07: 18~30) 여기로
        // 옮겨 재실행해도 유지되게 한 것 - 새 버튼에도 같은 패턴을 쓰려면 이 오버로드를 그대로 호출한다.
        /// <summary>
        /// 고정 줄 목록 행 안의 레이블 칸(가로 minX~maxX, 세로 전체, 자동 크기 10~maxFontSize) - 시설 화면 목록 4곳(무역품 판매·목록형 구매 후보·마구간 보유·상인조합)이
        /// 같은 조립을 각자 복사하고 있었다(Docs/Refactor/2026-10-26_씬전용.md H-4, SE-6). raycastTarget=false는 행 버튼 클릭을 레이블이 가로채지 않게 할 때만.
        /// </summary>
        public static TMP_Text EnsureRowLabel(Transform row, string name, float minX, float maxX, TextAlignmentOptions alignment, float maxFontSize = 22f, bool raycastTarget = true)
        {
            var go = GetOrCreateUIObject(row, name);
            SetAnchors(go.GetComponent<RectTransform>(), new Vector2(minX, 0f), new Vector2(maxX, 1f));
            var label = EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: maxFontSize);
            label.alignment = alignment;
            if (!raycastTarget) label.raycastTarget = false;
            return label;
        }

        public static TMP_Text EnsureLabel(Transform parent, string text, bool autoSize, float minFontSize, float maxFontSize)
        {
            var labelGo = GetOrCreateUIObject(parent, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            var label = EnsureText(labelGo);
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
            label.enableAutoSizing = autoSize;
            if (autoSize)
            {
                label.fontSizeMin = minFontSize;
                label.fontSizeMax = maxFontSize;
            }
            else
            {
                label.fontSize = maxFontSize;
            }
            return label;
        }

        // 인스톨러에서 TMP를 붙일 때는 GetOrAddComponent<TextMeshProUGUI> 대신 이것을 쓴다.
        // 비활성 계층(템플릿·닫힌 팝업)에 붙인 TMP는 Awake가 돌지 않아 미초기화 표시(글자 크기 -99)와 빈 폰트로 저장되고,
        // 런타임에 처음 켜질 때 TMP가 기본 설정을 다시 적용해 인스톨러가 넣은 자동 크기 범위·줄바꿈·raycastTarget을
        // 덮어쓴다(무역품 남은 수량 칸이 18~72로 커진 사례). 활성 상태에서 붙였을 때와 같은 기본값을 여기서 채워
        // 미초기화 표시를 없앤 뒤 호출자가 값을 덮어쓰게 한다.
        public static TextMeshProUGUI EnsureText(GameObject go)
        {
            var text = GetOrAddComponent<TextMeshProUGUI>(go);
            if (text.fontSize < 0f)
            {
                text.textWrappingMode = TMP_Settings.textWrappingMode;
                text.fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>(TMP_Settings.fontFeatures);
                text.extraPadding = TMP_Settings.enableExtraPadding;
                text.tintAllSprites = TMP_Settings.enableTintAllSprites;
                text.parseCtrlCharacters = TMP_Settings.enableParseEscapeCharacters;
                text.isTextObjectScaleStatic = TMP_Settings.isTextObjectScaleStatic;
                text.raycastTarget = TMP_Settings.enableRaycastTarget;
                text.fontSize = TMP_Settings.defaultFontSize;
                text.fontSizeMin = TMP_Settings.defaultFontSize * TMP_Settings.defaultTextAutoSizingMinRatio;
                text.fontSizeMax = TMP_Settings.defaultFontSize * TMP_Settings.defaultTextAutoSizingMaxRatio;
            }
            if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        /// <summary>
        /// 앵커 영역에 고정 크기 글자 칸을 get-or-create한다(입력 받지 않음). 대화 화면·이벤트 화면 빌더가 같은 모양을 써서 공용으로 뺐다(설계 89 §6).
        /// </summary>
        public static TextMeshProUGUI EnsureTextBlock(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = GetOrCreateUIObject(parent, name);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var text = EnsureText(go);
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 호버·길게 누름 툴팁 모양(Docs/설계/100번 §5.1)을 get-or-create한다. 상행 시작·숙소 휴식·골드 HUD가 같은 모양을 쓴다.
        /// 배경(Image)과 문구(TMP)는 부모·자식으로 나눈다 - 같은 오브젝트에 함께 붙이면 AddComponent가 실패한다(2026-09-21 사례).
        /// 높이는 문구 줄 수에 맞춘다. 입력을 가로채지 않게 레이캐스트를 끄고, 저장 상태는 비활성이다. 위치는 호출부가 정한다.
        /// </summary>
        public static (GameObject root, TMP_Text text) EnsureTooltip(Transform parent, string name, string rootId, string textId)
        {
            var root = GetOrCreateUIObject(parent, name);
            EnsureImage(root, new Color(0f, 0f, 0f, 1f)).raycastTarget = false;
            var layout = GetOrAddComponent<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = GetOrAddComponent<ContentSizeFitter>(root);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            EnsureMarker(root, rootId);

            var textGo = GetOrCreateUIObject(root.transform, "Text");
            var text = EnsureText(textGo);
            text.text = string.Empty;
            text.fontSize = 18f;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            EnsureMarker(textGo, textId);

            root.SetActive(false);
            return (root, text);
        }

        /// <summary>
        /// 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 버튼에 중계 2개(호버·길게 누름)를 붙이고, 같은 부모 아래 버튼 바로 위에
        /// 툴팁을 둔다(가로 xMin~xMax = 버튼 폭, 아래 변 = bottomY, 높이는 문구 줄 수). 같은 부모의 마지막 자식으로 올려 형제에 가려지지 않게 한다.
        /// 비활성 버튼도 진입·이탈·누름 이벤트를 받는다(설계 100번 §2).
        /// </summary>
        public static void EnsureButtonTooltip(RectTransform parent, GameObject button, string name, string rootId, string textId, float xMin, float xMax, float bottomY)
        {
            GetOrAddComponent<PointerHoverRelay>(button);
            GetOrAddComponent<PointerPressRelay>(button);
            var (tooltip, _) = EnsureTooltip(parent, name, rootId, textId);
            var rect = tooltip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, bottomY);
            rect.anchorMax = new Vector2(xMax, bottomY);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
            tooltip.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 시설 화면의 머리글 달린 패널(배경 + 위 10% 머리글 띠·제목). 숙소·상인조합 조립이 같은 모양을 각자 복사하던 것을 뺐다(2026-10-26 검진).
        /// </summary>
        public static RectTransform EnsureHeaderedPanel(RectTransform parent, string name, string title, Vector2 anchorMin, Vector2 anchorMax, Color panelColor, Color headerColor)
        {
            var panel = GetOrCreateUIObject(parent, name);
            var panelRect = panel.GetComponent<RectTransform>();
            SetAnchors(panelRect, anchorMin, anchorMax);
            EnsureImage(panel, panelColor);

            var header = GetOrCreateUIObject(panelRect, "Header");
            SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);
            EnsureImage(header, headerColor).raycastTarget = false;
            EnsureLabel(header.transform, title, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            return panelRect;
        }

        /// <summary>앵커 영역의 글자 버튼(배경 + 자동 축소 라벨 12~26 + 마커).</summary>
        public static GameObject EnsureLabeledButton(RectTransform parent, string name, string text, string id, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = GetOrCreateUIObject(parent, name);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EnsureImage(go, color);
            EnsureButton(go);
            EnsureLabel(go.transform, text, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EnsureMarker(go, id);
            return go;
        }

        /// <summary>앵커 영역의 자동 축소 글자 칸(10~maxFontSize). 런타임이 문구를 채우므로 마커는 TMP 오브젝트에 둔다.</summary>
        public static TMP_Text EnsureAnchoredLabel(RectTransform parent, string name, string id, Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment, float maxFontSize)
        {
            var go = GetOrCreateUIObject(parent, name);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: maxFontSize);
            label.alignment = alignment;
            EnsureMarker(label.gameObject, id);
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
        /// 공용 확인 대화상자(ConfirmDialogView, Docs/설계/79번 §7·§9.2)를 parent 아래에 get-or-create로 조립한다.
        /// Field(회수 적재 완료 확인)와 Hub(출발 경고)가 같은 구조를 씬마다 하나씩 두므로 특정 인스톨러가 아니라 여기에 둔다.
        /// 루트는 부모 전체를 덮는 반투명 막(뒤 UI 클릭 차단)이고, 문구는 Image가 있는 오브젝트와 분리해 자식 TMP에 둔다.
        /// 형제 중 마지막에 두어 같은 부모의 다른 요소보다 위에 그리며, 저장 상태는 비활성(Show 호출 시에만 표시)이다.
        /// </summary>
        public static ConfirmDialogView BuildConfirmDialog(Transform parent, string objectName, string elementId)
        {
            var root = GetOrCreateUIObject(parent, objectName);
            SetStretch(root.GetComponent<RectTransform>());
            EnsureImage(root, new Color(0f, 0f, 0f, 0.5f)).raycastTarget = true;
            EnsureMarker(root, elementId);

            var panel = GetOrCreateUIObject(root.transform, "Panel");
            SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.62f));
            EnsureImage(panel, new Color(0.96f, 0.95f, 0.92f, 1f));

            var message = GetOrCreateUIObject(panel.transform, "Message");
            SetAnchors(message.GetComponent<RectTransform>(), new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.95f));
            var messageLabel = EnsureLabel(message.transform, string.Empty, autoSize: true, minFontSize: 14f, maxFontSize: 26f);

            var confirmButton = BuildDialogButton(panel.transform, "ConfirmButton", new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.32f), new Color(0.75f, 0.87f, 1f, 1f), out var confirmLabel);
            var cancelButton = BuildDialogButton(panel.transform, "CancelButton", new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.32f), new Color(0.85f, 0.85f, 0.85f, 1f), out var cancelLabel);

            var view = GetOrAddComponent<ConfirmDialogView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("messageLabel").objectReferenceValue = messageLabel;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("cancelLabel").objectReferenceValue = cancelLabel;
            so.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
            so.ApplyModifiedProperties();

            root.transform.SetAsLastSibling();
            root.SetActive(false);
            return view;
        }

        private static Button BuildDialogButton(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Color color, out TMP_Text label)
        {
            var go = GetOrCreateUIObject(parent, objectName);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EnsureImage(go, color);
            var button = EnsureButton(go);
            label = EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            return button;
        }

        /// <summary>
        /// target을 anchor 바로 뒤 형제로 옮긴다. 먼저 맨 뒤로 보낸 다음 목표 인덱스를 읽는다 - target이 anchor보다 앞에 있는 상태에서
        /// 인덱스를 읽으면 target이 빠지며 한 칸 어긋난다.
        /// </summary>
        public static void PlaceSiblingAfter(Transform target, Transform anchor)
        {
            target.SetAsLastSibling();
            target.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
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

        // ==================== 씬 요소 조회·재배치(Docs/Refactor/2026-10-26_씬전용.md H-3 - Hub 인스톨러에서 옮김) ====================

        public static void DestroyComponentIfExists<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }

        public static void ReparentChildIfFound(Transform parent, string name, Transform newParent)
        {
            var child = parent.Find(name);
            if (child != null && newParent.Find(name) == null) child.SetParent(newParent, false);
        }

        public static void DestroyAllDirectChildrenNamed(Transform parent, string name)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        public static void ReparentIfFound(SceneUIRoot sceneUIRoot, string id, Transform newParent)
        {
            var found = FindMarker(sceneUIRoot, id);
            if (found == null)
            {
                Debug.LogWarning($"UI에서 '{id}' 요소를 찾을 수 없어 재배치를 건너뛴다. UIElementMarker가 부착되어 있는지 확인하라.");
                return;
            }

            if (found.transform.parent != newParent)
            {
                Undo.SetTransformParent(found.transform, newParent, $"Reparent {id}");
            }
        }

        // 씬에 원래부터 있던(코드로 만들어지지 않은) 버튼의 라벨 텍스트를 갱신할 때 쓴다 - "배치"
        // 버튼처럼 EditorUIBuilder.EnsureLabel로 만들어진 게 아니라 자식 이름이 "Label"이 아니므로,
        // EnsureLabel을 그대로 호출하면 새 라벨이 중복 생성된다. 대신 마커로 찾은 뒤 자식 TMP
        // 컴포넌트를 직접 갱신한다. 폰트 크기도 자동 축소(minFontSize~maxFontSize)로 맞춘다 - 인스펙터에서
        // 먼저 확정한 값(사용자 확정, 2026-09-07: 18~30)을 여기로 옮겨 재실행해도 유지되게 한다
        // (EditorUIBuilder.EnsureLabel의 autoSize 오버로드와 같은 값 의미).
        public static void SetLabelText(SceneUIRoot sceneUIRoot, string id, string text, float minFontSize, float maxFontSize)
        {
            var found = FindMarker(sceneUIRoot, id);
            if (found == null)
            {
                Debug.LogWarning($"UI에서 '{id}' 요소를 찾을 수 없어 라벨 텍스트를 갱신하지 못했다.");
                return;
            }

            var label = found.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                Debug.LogWarning($"'{id}' 요소 밑에서 TextMeshProUGUI 라벨을 찾을 수 없어 텍스트를 갱신하지 못했다.");
                return;
            }

            label.text = text;
            label.enableAutoSizing = true;
            label.fontSizeMin = minFontSize;
            label.fontSizeMax = maxFontSize;
        }

        public static UIElementMarker FindMarker(SceneUIRoot sceneUIRoot, string id)
        {
            foreach (var marker in sceneUIRoot.GetComponentsInChildren<UIElementMarker>(true))
            {
                if (marker.Id == id)
                {
                    return marker;
                }
            }
            return null;
        }
        public static Transform EnsureStretchLayer(Transform parent, string name, string markerId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(go, markerId);
            return go.transform;
        }
    }
}
