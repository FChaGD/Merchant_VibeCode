using System;

namespace Game.Core
{
    /// <summary>
    /// 배치(FormationLayout) 조회만 필요한 소비자를 위한 읽기 전용 계약. 상행 준비 UI의 편성 요약처럼
    /// 배치를 변경할 필요가 없는 곳은 이 인터페이스만 의존해 IFormationRepository.Apply에 대한
    /// 접근 권한을 아예 갖지 않도록 한다.
    /// </summary>
    public interface IFormationReader
    {
        bool TryLoadCurrent(out FormationLayout layout);

        /// <summary>
        /// 배치가 적용될 때마다 발생한다. 상행 준비 UI의 출발 조건(대열 연결)이 정비창에서 돌아올 때 패널이 다시 열린다는
        /// 보장 없이도 최신 배치를 따라가게 하려는 것(Docs/설계/79번 §15-13). 구독자는 Bootstrap 상주 저장소보다 짧게 살 수 있으니
        /// 재등록 시 해제 후 구독하고, 처리기에서 파괴된 씬 오브젝트를 확인해야 한다.
        /// </summary>
        event Action Changed;
    }
}
