using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    // DisplayLabel은 PartyTacticsPolicyStringsTableAsset으로 분리됐다(Docs/설계/18번 §6).
    [Serializable]
    public struct EnemyRecognitionOption
    {
        public EnemyRecognitionType Value;
        public int SortOrder;
        public bool IsDefault;
    }

    [Serializable]
    public struct ActivityRadiusOption
    {
        public ActivityRadiusPreset Value;
        public int SortOrder;
        public bool IsDefault;
    }

    [Serializable]
    public struct PursuitOption
    {
        public PursuitPreset Value;
        public int SortOrder;
        public bool IsDefault;
    }

    /// <summary>
    /// 파티 전체(역할군 그룹핑 없음) 방향성 지시 3축의 후보(값만) - 예전엔 TacticsPanel에
    /// static readonly 배열로 하드코딩돼 있었다(Docs/기획/14번 §3.6, Docs/설계/17번 §10). 역할군별로
    /// 갈리지 않아 RoleGroupTacticsCatalogAsset과 달리 RoleGroup 그룹핑 키가 없다. 임포터가 이미
    /// SortOrder로 정렬해서 저장하므로 소비자는 저장된 순서를 그대로 쓰면 된다. 화면 표시용 한국어
    /// 라벨은 PartyTacticsPolicyStringsTableAsset 조회로 분리됐다(Docs/설계/18번 §5/§6).
    /// </summary>
    [CreateAssetMenu(fileName = "PartyTacticsPolicyCatalog", menuName = "Game/Tactics/Party Tactics Policy Catalog")]
    public class PartyTacticsPolicyCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<EnemyRecognitionOption> recognitionOptions = new();
        [SerializeField] private List<ActivityRadiusOption> radiusOptions = new();
        [SerializeField] private List<PursuitOption> pursuitOptions = new();

        public IReadOnlyList<EnemyRecognitionOption> RecognitionOptions => recognitionOptions;
        public IReadOnlyList<ActivityRadiusOption> RadiusOptions => radiusOptions;
        public IReadOnlyList<PursuitOption> PursuitOptions => pursuitOptions;
    }
}
