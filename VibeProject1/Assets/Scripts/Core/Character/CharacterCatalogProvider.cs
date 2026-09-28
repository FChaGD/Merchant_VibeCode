using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 캐릭터 카탈로그를 전역 DI에 올린다(Docs/설계/54번 §3). 카탈로그를 RegisterSelf 단계에서 만드는 이유: 로스터·후보 제공자가
    /// 자기 ResolveDependencies에서 카탈로그 내용을 바로 읽으므로, 매니저 목록의 호출 순서와 관계없이 이미 채워져 있어야 한다.
    /// 전투는 이 카탈로그를 쓰지 않고 스탯 자산에서 직접 조회한다(전투 쪽 DI 구조를 바꾸지 않기 위해).
    /// </summary>
    public class CharacterCatalogProvider : MonoBehaviour, IManagedComponent
    {
        [SerializeField] private CharacterStatsTableAsset characterStatsTable;
        [SerializeField] private CharacterStringsTableAsset characterStringsTable;
        [SerializeField] private MercenaryClassStringsTableAsset mercenaryClassStringsTable;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            if (characterStatsTable == null)
            {
                Debug.LogWarning($"{nameof(CharacterCatalogProvider)}: 캐릭터 스탯 테이블이 배선되지 않았다(Play로 자동 임포트 후 Tools > Game > Build Bootstrap Scene).");
            }

            registrar.Register<ICharacterCatalogReader>(new TableCharacterCatalog(characterStatsTable, characterStringsTable, mercenaryClassStringsTable));
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }
    }
}
