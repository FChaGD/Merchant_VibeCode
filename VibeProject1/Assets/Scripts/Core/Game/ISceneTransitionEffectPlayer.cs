namespace Game.Core
{
    /// <summary>
    /// 씬 전환 요청 소비자(GameManager)를 위한 좁은 인터페이스(ISP) -
    /// ISceneTransitionContentRootRegistry(Wiring 전용)와 분리한다.
    /// </summary>
    public interface ISceneTransitionEffectPlayer
    {
        /// <summary>
        /// 현재 콘텐츠 씬의 화면을 슬라이드 아웃(+커튼 동행)한 뒤 실제 씬 전환을 수행하고,
        /// 대상 씬 로딩이 끝나면 커튼을 페이드 아웃한다(Docs/설계/10_씬전환_연출_아키텍처.md §6).
        /// </summary>
        void PlayTransition(ContentSceneId targetSceneId);
    }
}
