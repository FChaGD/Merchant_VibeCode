using System;

namespace Game.Core
{
    /// <summary>
    /// 시뮬레이션이 새로 만들어졌다는 사실만 알리는 좁은 계약 - 뷰 계층(BattleViewPresenter)이
    /// LiveBattleSimulationRule의 다른 멤버(Evaluate 등)를 몰라도 되게 한다(ISP).
    /// </summary>
    public interface IBattleSimulationEvents
    {
        event Action<BattleSimulationLoop> OnSimulationBuilt;
    }
}
