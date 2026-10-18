using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Field 이벤트 대화·선택 화면(설계 89 §6)을 ContentRoot 아래 get-or-create로 조립한다. FieldUIInstaller가 부른다 - 기능별 조립을 분리해 인스톨러 본문을
    /// 늘리지 않는다. 두 루트는 비활성으로 저장한다 - 패널(ViewRootPanel)이 연다. 반투명 배경이 입력을 막아 뒤의 이동 뷰 버튼이 눌리지 않는다.
    /// 그림·글자는 raycastTarget=false(대화 화면 클릭은 배경이 받는다). 배치 좌표는 설계 기준값 - Play 실측으로 겹침을 확인한다.
    /// </summary>
    internal static class FieldEventUIBuilder
    {
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color BubbleColor = new(0f, 0f, 0f, 0.8f);
        private static readonly Color BoxColor = new(0.1f, 0.1f, 0.12f, 0.9f);
        private static readonly Color OptionColor = new(0.95f, 0.92f, 0.85f, 1f);
        private static readonly Color ButtonColor = new(0.95f, 0.85f, 0.6f, 1f);
        private static readonly Color NameColor = new(1f, 0.9f, 0.6f);
        private static readonly Vector2 FormationMin = new(0.02f, 0.02f);
        private static readonly Vector2 FormationMax = new(0.14f, 0.08f);

        public static void Build(Transform parent, Transform placeAfter)
        {
            var dialogue = BuildDialogue(parent);
            var choice = BuildChoice(parent);
            if (placeAfter == null) return;
            EditorUIBuilder.PlaceSiblingAfter(dialogue, placeAfter);
            EditorUIBuilder.PlaceSiblingAfter(choice, dialogue);
        }

        private static Transform BuildDialogue(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "EventDialogue");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, FieldEventUIElementIds.DialogueRoot);

            var dim = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Dim");
            EditorUIBuilder.SetStretch(dim.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(dim, DimColor).raycastTarget = true;
            var catcher = EditorUIBuilder.GetOrAddComponent<FieldDialogueClickCatcher>(dim);

            var body = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Body");
            EditorUIBuilder.SetStretch(body.GetComponent<RectTransform>());

            // 스탠딩 왼쪽 x .04~.28, 오른쪽 x .72~.96, y .14~.86 - 말풍선보다 먼저 만들어 뒤에 그린다.
            BuildStanding(body.transform, "LeftStanding", new Vector2(0.04f, 0.14f), new Vector2(0.28f, 0.86f), flip: true, out var leftRoot, out var leftFigure, out var leftName);
            BuildStanding(body.transform, "RightStanding", new Vector2(0.72f, 0.14f), new Vector2(0.96f, 0.86f), flip: false, out var rightRoot, out var rightFigure, out var rightName);

            // 말풍선 x .30~.70, y .36~.62
            var bubble = EditorUIBuilder.GetOrCreateUIObject(body.transform, "Bubble");
            EditorUIBuilder.SetAnchors(bubble.GetComponent<RectTransform>(), new Vector2(0.30f, 0.36f), new Vector2(0.70f, 0.62f));
            EditorUIBuilder.EnsureImage(bubble, BubbleColor).raycastTarget = false;
            var speakerName = EditorUIBuilder.EnsureTextBlock(bubble.transform, "SpeakerName", new Vector2(0.03f, 0.80f), new Vector2(0.97f, 0.97f), 24f, TextAlignmentOptions.Left, NameColor);
            var line = EditorUIBuilder.EnsureTextBlock(bubble.transform, "Line", new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.80f), 26f, TextAlignmentOptions.TopLeft, Color.white);
            line.textWrappingMode = TextWrappingModes.Normal;
            var marker = EditorUIBuilder.EnsureTextBlock(bubble.transform, "ContinueMarker", new Vector2(0.93f, 0.02f), new Vector2(0.99f, 0.16f), 20f, TextAlignmentOptions.Center, Color.white);
            marker.text = "▼";
            // 꼬리: 말풍선 양옆 가운데의 작은 사각형 - 말하는 쪽만 켠다.
            var leftTail = Tail(bubble.transform, "LeftTail", new Vector2(-0.03f, 0.40f), new Vector2(0f, 0.60f));
            var rightTail = Tail(bubble.transform, "RightTail", new Vector2(1f, 0.40f), new Vector2(1.03f, 0.60f));

            var formation = FormationButton(root.transform);

            var view = EditorUIBuilder.GetOrAddComponent<FieldEventDialogueView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("clickCatcher").objectReferenceValue = catcher;
            so.FindProperty("speakerNameText").objectReferenceValue = speakerName;
            so.FindProperty("lineText").objectReferenceValue = line;
            so.FindProperty("continueMarker").objectReferenceValue = marker.gameObject;
            so.FindProperty("leftTail").objectReferenceValue = leftTail;
            so.FindProperty("rightTail").objectReferenceValue = rightTail;
            so.FindProperty("leftRoot").objectReferenceValue = leftRoot;
            so.FindProperty("leftFigure").objectReferenceValue = leftFigure;
            so.FindProperty("leftName").objectReferenceValue = leftName;
            so.FindProperty("rightRoot").objectReferenceValue = rightRoot;
            so.FindProperty("rightFigure").objectReferenceValue = rightFigure;
            so.FindProperty("rightName").objectReferenceValue = rightName;
            so.FindProperty("formationButton").objectReferenceValue = formation;
            so.ApplyModifiedProperties();

            body.SetActive(false);
            root.SetActive(false);
            return root.transform;
        }

        private static Transform BuildChoice(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "EventChoice");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, FieldEventUIElementIds.ChoiceRoot);

            var dim = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Dim");
            EditorUIBuilder.SetStretch(dim.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(dim, DimColor).raycastTarget = true;

            // 설명문 x .15~.85, y .66~.90
            var box = EditorUIBuilder.GetOrCreateUIObject(root.transform, "DescriptionBox");
            EditorUIBuilder.SetAnchors(box.GetComponent<RectTransform>(), new Vector2(0.15f, 0.66f), new Vector2(0.85f, 0.90f));
            EditorUIBuilder.EnsureImage(box, BoxColor).raycastTarget = false;
            var description = EditorUIBuilder.EnsureTextBlock(box.transform, "Description", new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), 26f, TextAlignmentOptions.TopLeft, Color.white);
            description.textWrappingMode = TextWrappingModes.Normal;

            // 표시 인물 x .15~.85, y .40~.62 - 가로 정렬
            var peopleRow = EditorUIBuilder.GetOrCreateUIObject(root.transform, "People");
            EditorUIBuilder.SetAnchors(peopleRow.GetComponent<RectTransform>(), new Vector2(0.15f, 0.40f), new Vector2(0.85f, 0.62f));
            var peopleLayout = EditorUIBuilder.GetOrAddComponent<HorizontalLayoutGroup>(peopleRow);
            peopleLayout.spacing = 24f;
            peopleLayout.childAlignment = TextAnchor.MiddleCenter;
            peopleLayout.childControlWidth = true;
            peopleLayout.childControlHeight = true;
            peopleLayout.childForceExpandWidth = false;
            peopleLayout.childForceExpandHeight = true;
            var person = EditorUIBuilder.GetOrCreateUIObject(peopleRow.transform, "PersonTemplate");
            EditorUIBuilder.GetOrAddComponent<LayoutElement>(person).preferredWidth = 160f;
            var figure = EditorUIBuilder.GetOrCreateUIObject(person.transform, "Figure");
            EditorUIBuilder.SetAnchors(figure.GetComponent<RectTransform>(), new Vector2(0f, 0.18f), new Vector2(1f, 1f));
            var figureImage = EditorUIBuilder.EnsureImage(figure, Color.white);
            figureImage.raycastTarget = false;
            var personName = EditorUIBuilder.EnsureTextBlock(person.transform, "Name", new Vector2(0f, 0f), new Vector2(1f, 0.16f), 20f, TextAlignmentOptions.Center, Color.white);
            // 줄 표시 컴포넌트가 하위 요소를 직렬화 참조로 든다 - 화면이 이름 문자열로 찾지 않게(설계 89 §6.3, 2026-10-16 검진).
            var personView = EditorUIBuilder.GetOrAddComponent<FieldEventPersonView>(person);
            var personSo = new SerializedObject(personView);
            personSo.FindProperty("figure").objectReferenceValue = figureImage;
            personSo.FindProperty("nameText").objectReferenceValue = personName;
            personSo.ApplyModifiedProperties();
            person.SetActive(false);

            // 선택지 x .20~.80, y .08~.36
            var optionsRoot = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Options");
            EditorUIBuilder.SetAnchors(optionsRoot.GetComponent<RectTransform>(), new Vector2(0.20f, 0.08f), new Vector2(0.80f, 0.36f));
            var optionsLayout = EditorUIBuilder.GetOrAddComponent<VerticalLayoutGroup>(optionsRoot);
            optionsLayout.spacing = 6f;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
            var option = EditorUIBuilder.GetOrCreateUIObject(optionsRoot.transform, "OptionTemplate");
            EditorUIBuilder.EnsureImage(option, OptionColor);
            var optionButton = EditorUIBuilder.EnsureButton(option);
            var optionGroup = EditorUIBuilder.GetOrAddComponent<CanvasGroup>(option);
            EditorUIBuilder.GetOrAddComponent<LayoutElement>(option).preferredHeight = 44f;
            var optionLabel = EditorUIBuilder.EnsureLabel(option.transform, string.Empty, autoSize: true, minFontSize: 16f, maxFontSize: 26f);
            var optionView = EditorUIBuilder.GetOrAddComponent<FieldEventOptionView>(option);
            var optionSo = new SerializedObject(optionView);
            optionSo.FindProperty("button").objectReferenceValue = optionButton;
            optionSo.FindProperty("label").objectReferenceValue = optionLabel;
            optionSo.FindProperty("group").objectReferenceValue = optionGroup;
            optionSo.ApplyModifiedProperties();
            option.SetActive(false);

            var formation = FormationButton(root.transform);

            var view = EditorUIBuilder.GetOrAddComponent<FieldEventChoiceView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("personRowTemplate").objectReferenceValue = personView;
            so.FindProperty("optionRowTemplate").objectReferenceValue = optionView;
            so.FindProperty("formationButton").objectReferenceValue = formation;
            so.ApplyModifiedProperties();

            root.SetActive(false);
            return root.transform;
        }

        // "정비창 보기"(설계 89 §6.3) - 두 화면 모두 좌하단. 배경보다 위(마지막 형제)에 둔다.
        private static Button FormationButton(Transform parent)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "FormationViewButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), FormationMin, FormationMax);
            EditorUIBuilder.EnsureImage(go, ButtonColor);
            var button = EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "정비창 보기", autoSize: true, minFontSize: 16f, maxFontSize: 26f);
            go.transform.SetAsLastSibling();
            return button;
        }

        // 이벤트 화면 스탠딩은 대화 화면보다 크게(높이 72%) 그려 이름 칸 비율이 다르다 - 대화 화면 빌더와 모양을 공유하지 않는다.
        private static void BuildStanding(Transform parent, string name, Vector2 min, Vector2 max, bool flip, out GameObject root, out Image figure, out TMP_Text nameText)
        {
            root = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), min, max);
            var figureGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Figure");
            var figureRect = figureGo.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(figureRect, new Vector2(0f, 0.08f), new Vector2(1f, 1f));
            // 일러1(왼쪽)만 그림을 좌우반전한다(설계 88 §5.2와 같음) - 이름은 별도 요소라 반전되지 않는다.
            figureRect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            figure = EditorUIBuilder.EnsureImage(figureGo, Color.white);
            figure.raycastTarget = false;
            nameText = EditorUIBuilder.EnsureTextBlock(root.transform, "Name", new Vector2(0f, 0f), new Vector2(1f, 0.07f), 22f, TextAlignmentOptions.Center, Color.white);
            root.SetActive(false);
        }

        private static GameObject Tail(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            EditorUIBuilder.EnsureImage(go, BubbleColor).raycastTarget = false;
            go.SetActive(false);
            return go;
        }
    }
}
