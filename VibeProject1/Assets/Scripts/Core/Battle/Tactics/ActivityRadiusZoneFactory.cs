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
        public static IActivityRadiusZone Create(string preset, Vector2 homePosition, float standardRadius)
        {
            return preset switch
            {
                "FormationHold" => new FixedActivityRadiusZone(homePosition),
                "TripWide" => new StandardActivityRadiusZone(standardRadius),
                "FieldWide" => new WideActivityRadiusZone(),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}
