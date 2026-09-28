using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을별 고용 후보 시스템이 아직 없어(Docs/기획/53번 §6) "카탈로그의 캐릭터 중 아직 고용하지 않은 캐릭터 전부"를 테이블 순서로
    /// 돌려주는 임시 제공자. 마을·시설 Id는 받지만 무시한다. 실제 후보 시스템 설계 후 대체/제거 대상이다.
    /// 고용 여부가 바뀔 때마다 결과가 달라지므로 캐시하지 않는다(후보는 최대 15명).
    /// </summary>
    public class PlaceholderMercenaryCandidateProvider : MonoBehaviour, IMercenaryCandidateReader, IManagedComponent
    {
        private ICharacterCatalogReader catalog;
        private IHiredCharacterRoster roster;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IMercenaryCandidateReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve(out catalog);
            registrar.TryResolve(out roster);
        }

        public IReadOnlyList<CharacterProfile> GetCandidates(int cityId, string facilityId)
        {
            if (catalog == null) return Array.Empty<CharacterProfile>();

            var candidates = new List<CharacterProfile>();
            foreach (var profile in catalog.All)
            {
                if (roster != null && roster.IsHired(profile.CharacterId)) continue;
                candidates.Add(profile);
            }
            return candidates;
        }
    }
}
