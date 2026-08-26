using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 씬 배선(HubUIWiring/FieldUIWiring) 전용 좁은 인터페이스(ISP) -
    /// ISceneTransitionEffectPlayer(GameManager 전용)와 분리한다. 씬이 로드될 때마다 그 씬의
    /// "슬라이드 대상 콘텐츠 루트"를 등록해 두면, 다음 전환 시점에 SceneTransitionEffectController가
    /// 이 값을 그대로 슬라이드시킨다.
    /// </summary>
    public interface ISceneTransitionContentRootRegistry
    {
        void RegisterContentRoot(ContentSceneId sceneId, RectTransform contentRoot);
    }
}
