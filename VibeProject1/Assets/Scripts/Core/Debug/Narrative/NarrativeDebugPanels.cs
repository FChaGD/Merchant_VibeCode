#if UNITY_EDITOR
namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 이력 디버그(EncounterDebugView)와 대화 디버그(DialogueDebugView)는 오른쪽 위 같은 자리에 창을 펼친다. 하나를 펼치면 다른 하나를 접는다 -
    /// 둘이 겹쳐 그려지지 않게. 디버그 도구를 걷어낼 때 두 뷰와 함께 지운다.
    /// </summary>
    public static class NarrativeDebugPanels
    {
        public const string History = "history";
        public const string Dialogue = "dialogue";

        public static string Expanded { get; set; }

        public static void Toggle(string panel) => Expanded = Expanded == panel ? null : panel;
    }
}
#endif
