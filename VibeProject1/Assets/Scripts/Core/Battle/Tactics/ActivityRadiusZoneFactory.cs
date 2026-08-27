using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ActivityRadiusPreset→구현체 매핑을 한 곳에 모은다(OCP) - 새 프리셋이 늘어도 이 스위치에만
    /// 케이스를 추가하면 되고, 소비자(IUnitTacticsProfileResolver)는 무변경이다.
    /// </summary>
    public static class ActivityRadiusZoneFactory
    {
        public static IActivityRadiusZone Create(ActivityRadiusPreset preset, Vector2 homePosition, float standardRadius)
        {
            return preset switch
            {
                ActivityRadiusPreset.Fixed => new FixedActivityRadiusZone(homePosition),
                ActivityRadiusPreset.Standard => new StandardActivityRadiusZone(standardRadius),
                ActivityRadiusPreset.Wide => new WideActivityRadiusZone(),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}
