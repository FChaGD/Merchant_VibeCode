using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Field 상행 대화 화면(설계 88 §5·§6·§7)을 이동 뷰 아래에 get-or-create로 조립한다. FieldUIInstaller가 부른다 - TacticsUIBuilder처럼 기능별 조립을 분리해
    /// 인스톨러 본문을 늘리지 않는다. 형제 순서가 입력을 가른다: 클릭 받기는 배경 바로 뒤(버튼들보다 아래), 대화 화면 루트는 맨 뒤(선택지가 위).
    /// 대화 화면의 그림·글자는 raycastTarget=false - 빈 곳 클릭이 클릭 받기까지 내려가야 한다.
    /// </summary>
    internal static class FieldDialogueUIBuilder
    {
        private static readonly Color BubbleColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color OptionColor = new(0.95f, 0.92f, 0.85f, 1f);
        private static readonly Color ListColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color TalkButtonColor = new(0.95f, 0.85f, 0.6f, 1f);

        public static void Build(Transform movementViewRoot, Transform background)
        {
            var catcher = BuildClickCatcher(movementViewRoot, background);
            BuildManualTalk(movementViewRoot);
            BuildDialogueRoot(movementViewRoot, catcher);
        }

        private static FieldDialogueClickCatcher BuildClickCatcher(Transform parent, Transform background)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, "DialogueClickCatcher");
            EditorUIBuilder.SetStretch(go.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureImage(go, new Color(1f, 1f, 1f, 0.001f));   // 투명이어도 raycastTarget이면 클릭을 받는다
            var catcher = EditorUIBuilder.GetOrAddComponent<FieldDialogueClickCatcher>(go);
            EditorUIBuilder.EnsureMarker(go, FieldDialogueUIElementIds.ClickCatcher);
            if (background != null) EditorUIBuilder.PlaceSiblingAfter(go.transform, background);
            go.SetActive(false);
            return catcher;
        }

        private static void BuildDialogueRoot(Transform parent, FieldDialogueClickCatcher catcher)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "DialogueView");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            EditorUIBuilder.EnsureMarker(root, FieldDialogueUIElementIds.Root);

            var body = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Body");
            EditorUIBuilder.SetStretch(body.GetComponent<RectTransform>());

            // 말풍선 x .34~.66, y .30~.44
            var bubble = EditorUIBuilder.GetOrCreateUIObject(body.transform, "Bubble");
            EditorUIBuilder.SetAnchors(bubble.GetComponent<RectTransform>(), new Vector2(0.34f, 0.30f), new Vector2(0.66f, 0.44f));
            EditorUIBuilder.EnsureImage(bubble, BubbleColor).raycastTarget = false;
            var speakerName = Text(bubble.transform, "SpeakerName", new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.97f), 22f, TextAlignmentOptions.Left, new Color(1f, 0.9f, 0.6f));
            var line = Text(bubble.transform, "Line", new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.72f), 24f, TextAlignmentOptions.TopLeft, Color.white);
            line.textWrappingMode = TextWrappingModes.Normal;
            var marker = Text(bubble.transform, "ContinueMarker", new Vector2(0.92f, 0.02f), new Vector2(0.99f, 0.2f), 18f, TextAlignmentOptions.Center, Color.white);
            marker.text = "▼";

            // 스탠딩: 일러1(왼쪽, 그림만 좌우반전) x .16~.32, 일러2(오른쪽, 원본) x .68~.84, y .06~.34
            BuildStanding(body.transform, "LeftStanding", new Vector2(0.16f, 0.06f), new Vector2(0.32f, 0.34f), flip: true, out var leftRoot, out var leftFigure, out var leftName);
            BuildStanding(body.transform, "RightStanding", new Vector2(0.68f, 0.06f), new Vector2(0.84f, 0.34f), flip: false, out var rightRoot, out var rightFigure, out var rightName);

            // 선택지 x .34~.66, y .14~.28 / 남은 시간 막대 y .12~.135
            var options = EditorUIBuilder.GetOrCreateUIObject(body.transform, "Options");
            EditorUIBuilder.SetAnchors(options.GetComponent<RectTransform>(), new Vector2(0.34f, 0.14f), new Vector2(0.66f, 0.28f));
            var layout = EditorUIBuilder.GetOrAddComponent<VerticalLayoutGroup>(options);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var optionTemplate = BuildRowTemplate(options.transform, "OptionTemplate", OptionColor, 32f);

            var timer = EditorUIBuilder.GetOrCreateUIObject(body.transform, "TimerBar");
            EditorUIBuilder.SetAnchors(timer.GetComponent<RectTransform>(), new Vector2(0.34f, 0.12f), new Vector2(0.66f, 0.135f));
            var timerFill = EditorUIBuilder.EnsureImage(timer, new Color(0.95f, 0.75f, 0.2f, 1f));
            timerFill.sprite = EditorUIBuilder.GetOrCreateSolidSprite();
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.raycastTarget = false;

            var view = EditorUIBuilder.GetOrAddComponent<FieldDialogueView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("clickCatcher").objectReferenceValue = catcher;
            so.FindProperty("speakerNameText").objectReferenceValue = speakerName;
            so.FindProperty("lineText").objectReferenceValue = line;
            so.FindProperty("continueMarker").objectReferenceValue = marker.gameObject;
            so.FindProperty("leftRoot").objectReferenceValue = leftRoot;
            so.FindProperty("leftFigure").objectReferenceValue = leftFigure;
            so.FindProperty("leftName").objectReferenceValue = leftName;
            so.FindProperty("rightRoot").objectReferenceValue = rightRoot;
            so.FindProperty("rightFigure").objectReferenceValue = rightFigure;
            so.FindProperty("rightName").objectReferenceValue = rightName;
            so.FindProperty("optionsRoot").objectReferenceValue = options;
            so.FindProperty("optionTemplate").objectReferenceValue = optionTemplate;
            so.FindProperty("timerFill").objectReferenceValue = timerFill;
            so.ApplyModifiedProperties();

            body.SetActive(false);
        }

        private static void BuildStanding(Transform parent, string name, Vector2 min, Vector2 max, bool flip, out GameObject root, out Image figure, out TMP_Text nameText)
        {
            root = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(root.GetComponent<RectTransform>(), min, max);

            var figureGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "Figure");
            var figureRect = figureGo.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(figureRect, new Vector2(0f, 0.15f), new Vector2(1f, 1f));
            // 원본 일러스트는 오른쪽 자리 기준 방향이라 왼쪽만 그림을 뒤집는다(설계 88 §5.2) - 이름은 별도 요소라 반전되지 않는다.
            figureRect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            figure = EditorUIBuilder.EnsureImage(figureGo, Color.white);
            figure.raycastTarget = false;

            nameText = Text(root.transform, "Name", new Vector2(0f, 0f), new Vector2(1f, 0.14f), 20f, TextAlignmentOptions.Center, Color.white);
            root.SetActive(false);
        }

        private static void BuildManualTalk(Transform parent)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(parent, "ManualTalk");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, FieldDialogueUIElementIds.ManualTalk);

            // "대화" 버튼: 좌하단 버튼 열 맨 위(방향성 지시 y .12~.20 위) x .02~.14, y .22~.30
            var buttonGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "TalkButton");
            EditorUIBuilder.SetAnchors(buttonGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.22f), new Vector2(0.14f, 0.30f));
            EditorUIBuilder.EnsureImage(buttonGo, TalkButtonColor);
            var talkButton = EditorUIBuilder.EnsureButton(buttonGo);
            EditorUIBuilder.EnsureLabel(buttonGo.transform, "대화", autoSize: true, minFontSize: 18f, maxFontSize: 30f);

            // 상단원 목록: 버튼 오른쪽 x .15~.33, y .22~.52, 세로 스크롤
            var list = EditorUIBuilder.GetOrCreateUIObject(root.transform, "MemberList");
            EditorUIBuilder.SetAnchors(list.GetComponent<RectTransform>(), new Vector2(0.15f, 0.22f), new Vector2(0.33f, 0.52f));
            EditorUIBuilder.EnsureImage(list, ListColor);
            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(list.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var layout = EditorUIBuilder.GetOrAddComponent<VerticalLayoutGroup>(content);
            layout.spacing = 4f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            EditorUIBuilder.GetOrAddComponent<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            EditorUIBuilder.ConfigureScrollRect(list, viewport, contentRect, horizontal: false, vertical: true);
            var memberTemplate = BuildRowTemplate(content.transform, "MemberTemplate", OptionColor, 34f);
            list.SetActive(false);

            var view = EditorUIBuilder.GetOrAddComponent<FieldManualTalkView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("talkButton").objectReferenceValue = talkButton;
            so.FindProperty("memberList").objectReferenceValue = list;
            so.FindProperty("memberTemplate").objectReferenceValue = memberTemplate;
            so.ApplyModifiedProperties();
        }

        // 선택지·상단원 줄 템플릿 - 비활성으로 저장하고 화면이 복제해 쓴다. 흐림 표시용 CanvasGroup을 붙인다.
        private static Button BuildRowTemplate(Transform parent, string name, Color color, float height)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.EnsureImage(go, color);
            var button = EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.GetOrAddComponent<CanvasGroup>(go);
            EditorUIBuilder.GetOrAddComponent<LayoutElement>(go).preferredHeight = height;
            EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 14f, maxFontSize: 22f);
            go.SetActive(false);
            return button;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment, Color color)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            var text = EditorUIBuilder.EnsureText(go);
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
