using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정비창 슬롯 좌표→전장 좌표 변환만 담당한다. 스폰/반지름 계산(IBattleFieldGeometry)과
    /// 분리한 이유는 06번 설계 문서 §10에 저우선순위 backlog로 남아있던 "두 책임 겸임" 문제를
    /// 방향성 지시 축(활동 반경)이 세 번째 반지름 계산을 얹기 전에 정리한 것이다
    /// (Docs/설계/12번 §5.2).
    /// </summary>
    public interface IAllyPositionLayout
    {
        Vector2 ComputeAllyPosition(int column, int row, int columnCount);
    }
}
