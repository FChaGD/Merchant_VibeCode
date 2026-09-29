using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 디버그 핀 목록(Docs/기획/59번 §4.5, 설계 60번 §8). 편집 정책(Hub/Field 배치 패널)이 대열 영역 계산에 핀을 더할 때 이 계약만 본다 - 구현
    /// (FormationDebugPinStore)은 에디터 전용 디버그 도구라, 설치하지 않았거나 빌드에서는 없는 것이 정상이다(null 허용).
    /// </summary>
    public interface IFormationDebugAreaSource
    {
        IReadOnlyList<FormationAreaPin> Pins { get; }

        // 같은 칸에 이미 핀이 있으면 반경만 바꾼다.
        void AddOrReplace(FormationAreaPin pin);

        bool Remove(int slotIndex);
    }
}
