namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 씬 이름 상수. SceneManager는 씬 이름으로 로드/언로드하므로 매직 스트링을 여기로 모은다.
    /// nameof(ContentSceneId.Xxx)로 정의해 enum 멤버 이름과 항상 같은 값을 갖도록 강제한다 - enum이
    /// 리네임되면 여기도 컴파일 에러로 드러나 두 정의가 따로 어긋날 수 없다(ContentSceneId가 유일한
    /// 콘텐츠 씬 식별자 출처, Docs/Refactor/2026-08-26-공통.md 3단계 수정안).
    /// </summary>
    public static class SceneNames
    {
        public const string Hub = nameof(ContentSceneId.Hub);
        public const string Field = nameof(ContentSceneId.Field);
    }
}
