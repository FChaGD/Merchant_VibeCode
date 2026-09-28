using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설 카탈로그를 전역 DI에 올린다(Docs/설계/56번 §3). CharacterCatalogProvider와 같은 이유로 RegisterSelf 단계에서
    /// 카탈로그를 만든다 - 로스터가 자기 ResolveDependencies에서 카탈로그 내용을 바로 읽는다.
    /// </summary>
    public class CaravanAssetCatalogProvider : MonoBehaviour, IManagedComponent
    {
        [SerializeField] private CaravanAssetTableAsset wagonTable;
        [SerializeField] private CaravanAssetStringsTableAsset wagonStrings;
        [SerializeField] private CaravanAssetTableAsset facilityTable;
        [SerializeField] private CaravanAssetStringsTableAsset facilityStrings;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            if (wagonTable == null || facilityTable == null)
            {
                Debug.LogWarning($"{nameof(CaravanAssetCatalogProvider)}: 마차·시설 테이블이 배선되지 않았다(Play로 자동 임포트 후 Tools > Game > Build Bootstrap Scene).");
            }

            registrar.Register<ICaravanAssetCatalogReader>(new TableCaravanAssetCatalog(wagonTable, wagonStrings, facilityTable, facilityStrings));
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }
    }
}
