using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을별 마차·시설 판매 시스템이 아직 없어 "카탈로그의 개체 중 아직 보유하지 않은 것 전부"를 테이블 순서로 돌려주는 임시
    /// 제공자(Docs/설계/56번 §6). 마을·시설 Id는 받지만 무시한다. 실제 판매 시스템 설계 후 대체/제거 대상이다.
    /// 보유 여부가 바뀔 때마다 결과가 달라지므로 캐시하지 않는다(후보는 최대 10개).
    /// </summary>
    public class PlaceholderCaravanAssetCandidateProvider : MonoBehaviour, ICaravanAssetCandidateReader, IManagedComponent
    {
        private ICaravanAssetCatalogReader catalog;
        private IOwnedCaravanAssetRoster roster;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ICaravanAssetCandidateReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve(out catalog);
            registrar.TryResolve(out roster);
        }

        public IReadOnlyList<CaravanAssetProfile> GetCandidates(int cityId, string facilityId)
        {
            if (catalog == null) return Array.Empty<CaravanAssetProfile>();

            var candidates = new List<CaravanAssetProfile>();
            foreach (var profile in catalog.All)
            {
                candidates.Add(profile);
            }
            return candidates;
        }
    }
}
