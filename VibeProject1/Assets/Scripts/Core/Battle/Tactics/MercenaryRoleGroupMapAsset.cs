using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct MercenaryRoleGroupEntry
    {
        public MercenaryClass MercenaryClass;
        public RoleGroup RoleGroup;
    }

    /// <summary>
    /// 직업(MercenaryClass)은 그대로 유지하고 그 위에 "역할군이 무엇인가"만 추가로 정의하는
    /// 에디터 편집 가능 데이터(Docs/설계/12번 §5.3) - 새 직업/역할군이 생겨도 코드 수정 없이
    /// 이 에셋만 편집하면 된다. 코드에 정적 Dictionary로 박아두지 않은 이유는 그 반대(직업 수가
    /// 적어 안 바뀐다는 확정)를 전제하지 않기 위함이다.
    /// </summary>
    [CreateAssetMenu(fileName = "MercenaryRoleGroupMap", menuName = "Game/Tactics/Mercenary Role Group Map")]
    public class MercenaryRoleGroupMapAsset : ScriptableObject
    {
        [SerializeField] private List<MercenaryRoleGroupEntry> entries = new();

        public bool TryGetRoleGroup(MercenaryClass mercenaryClass, out RoleGroup roleGroup)
        {
            foreach (var entry in entries)
            {
                if (entry.MercenaryClass == mercenaryClass)
                {
                    roleGroup = entry.RoleGroup;
                    return true;
                }
            }

            roleGroup = default;
            return false;
        }
    }
}
