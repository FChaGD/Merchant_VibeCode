using System;

namespace Game.Core
{
    /// <summary>
    /// 씬 전환 연출(SceneTransitionEffectController)의 커튼 페이드 아웃이 완전히 끝난 시점을 알리는
    /// 좁은 인터페이스(ISP) - Hub/Field UI 컨트롤러가 "화면이 다 드러난 뒤에만 상호작용을 허용"하려는
    /// 용도로만 쓰므로 PlayTransition/RegisterContentRoot는 노출하지 않는다. 전환 없이 로드된 경우
    /// (최초 진입, 디버그 재진입 등)는 커튼이 뜬 적이 없으므로 즉시 발생한다.
    /// </summary>
    public interface ISceneRevealSignal
    {
        event Action<ContentSceneId> SceneRevealed;
    }
}
