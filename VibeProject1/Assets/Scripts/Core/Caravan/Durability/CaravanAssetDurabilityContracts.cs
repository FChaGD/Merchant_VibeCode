using System;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 개체별 내구도 읽기(Docs/설계/91번 §3.2). 전투 시작·마구간 표시·정산 소비자는 읽기만 하므로 쓰기와 나눴다(ISP).
    /// </summary>
    public interface ICaravanAssetDurabilityReader
    {
        /// <summary>보유 개체의 현재·최대 내구도. 최대치는 테이블 값이고 현재값은 그 안으로 잘린다(기획 90번 §4-5). 기록이 없으면 false.</summary>
        bool TryGet(string instanceId, out float current, out int max);

        /// <summary>값 변경·개체 등록·삭제 모두에서 발생한다.</summary>
        event Action Changed;
    }

    /// <summary>
    /// 내구도 쓰기(정산·수리·이벤트·디버그). 0이 돼도 기록을 지우지 않는다 - 개체 제거는 보유 목록 쪽 경로(공용 파괴 처리)가 하고,
    /// 기록은 보유 변경 알림으로 따라 지워진다.
    /// </summary>
    public interface ICaravanAssetDurabilityWriter
    {
        /// <summary>현재값을 0~최대로 잘라 저장한다. 기록이 없으면 false.</summary>
        bool TrySet(string instanceId, float value);

        /// <summary>최대치로 복원(수리). 기록이 없으면 false.</summary>
        bool TryRestore(string instanceId);
    }
}
