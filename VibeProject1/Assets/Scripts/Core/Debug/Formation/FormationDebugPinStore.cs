#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 대열을 임의로 만드는 디버그 핀 목록(Docs/기획/59번 §4.5, 설계 60번 §8). Play 세션 동안만 유지된다.
    /// Hub/Field 배치 패널(HubFormationPanel/FieldFormationPanel)과 같은 UIManager 오브젝트에 붙여, 두 패널이 GetComponent로 직접 찾게
    /// 한다(전역 DI 대상 아님 - 매니저 종속 하위 컴포넌트 규칙). 마을·상행 정비창이 같은 핀을 본다.
    /// 설치/제거는 FormationDebugPinInstaller(Tools/Game/Debug/Install|Remove/Formation Debug Pins)가 담당한다. 걷어낼 때는 Remove 메뉴를 먼저
    /// 실행한 뒤 이 파일·FormationDebugPinHandle·FormationGridDebugView와 설치기를 지운다.
    /// </summary>
    public class FormationDebugPinStore : MonoBehaviour, IFormationDebugAreaSource
    {
        private readonly List<FormationAreaPin> pins = new();

        public IReadOnlyList<FormationAreaPin> Pins => pins;

        public void AddOrReplace(FormationAreaPin pin)
        {
            Remove(pin.SlotIndex);
            pins.Add(pin);
        }

        public bool Remove(int slotIndex) => pins.RemoveAll(p => p.SlotIndex == slotIndex) > 0;
    }
}
#endif
