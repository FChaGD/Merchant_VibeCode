namespace Game.Core
{
    /// <summary>
    /// 툴팁 하나의 표시 판정. 화면 상태("버튼이 비활성일 때만" 등)는 모르고, 사용처가 문구를 비우는 것으로 표현한다(Docs/설계/100번 §4.1).
    /// 각 입력 변경은 화면 반영이 필요한지(표시 여부가 바뀌었거나 보이는 중 문구가 바뀜)를 돌려준다.
    /// </summary>
    public sealed class TooltipVisibilityState
    {
        private bool hovering;
        private bool longPressing;

        public string Text { get; private set; } = string.Empty;
        public bool IsVisible => (hovering || longPressing) && !string.IsNullOrEmpty(Text);

        public bool SetHovering(bool value)
        {
            var wasVisible = IsVisible;
            hovering = value;
            return wasVisible != IsVisible;
        }

        public bool SetLongPressing(bool value)
        {
            var wasVisible = IsVisible;
            longPressing = value;
            return wasVisible != IsVisible;
        }

        public bool SetText(string value)
        {
            value ??= string.Empty;
            if (value == Text) return false;

            var wasVisible = IsVisible;
            Text = value;
            return wasVisible != IsVisible || IsVisible;
        }
    }
}
