using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>개체마다 표시 이름이 다른 유닛(Docs/설계/81번 §5.3). 정비창 정보 패널만 이 이름을 쓴다 - DisplayName은 팔레트 카테고리 이름이라 종류명으로 둔다.</summary>
    public interface IInstanceNamedUnit
    {
        string InstanceName { get; }
    }

    /// <summary>
    /// 보유 마차·시설 유닛. 이름은 조회할 때마다 로스터에서 계산한다 - 다른 개체가 제거되면 번호가 당겨지므로(기획 80번 §4-1) 생성 시 고정하면 틀린다.
    /// </summary>
    public sealed class CaravanAssetFormationUnit : PlaceholderFormationUnit, IInstanceNamedUnit
    {
        private readonly Func<string> instanceName;

        public CaravanAssetFormationUnit(string id, string kindLabel, Sprite icon, FormationUnitKind kind, FormationAreaShape areaShape, Func<string> instanceName)
            : base(id, kindLabel, icon, kind, areaShape)
        {
            this.instanceName = instanceName;
        }

        public string InstanceName => instanceName?.Invoke() ?? DisplayName;
    }
}
