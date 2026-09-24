using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을별 시설 데이터 시스템이 아직 없어 모든 마을에 같은 제공 여부를 돌려주는 임시 제공자.
    /// 실제 시스템 설계 후 대체/제거 대상이다. 인스펙터 체크 목록은 "시설이 빠졌을 때 버튼이 빈칸 없이
    /// 당겨지는지"를 코드 수정 없이 검증하기 위한 것이다(Docs/설계/37번 §5.1) - 목록 항목은
    /// ManagerHierarchyInstaller가 TownFacilityCatalog 기준으로 채운다.
    /// </summary>
    public class PlaceholderTownFacilityAvailabilityProvider : MonoBehaviour, ITownFacilityAvailabilityReader, IManagedComponent
    {
        [Serializable]
        private class FacilityToggle
        {
            public string facilityId;
            public bool available = true;
        }

        [SerializeField] private List<FacilityToggle> facilities = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITownFacilityAvailabilityReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }

        public bool IsFacilityAvailable(int cityId, string facilityId)
        {
            foreach (var toggle in facilities)
            {
                if (toggle.facilityId == facilityId)
                {
                    return toggle.available;
                }
            }

            // 목록에 없는 시설(인스톨러 미실행 등)은 기본 제공으로 본다 - 현재 동작(전부 노출)을 보존한다.
            return true;
        }
    }
}
