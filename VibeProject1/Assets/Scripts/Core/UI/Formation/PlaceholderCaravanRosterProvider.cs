using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트·전투·고용 화면이 함께 쓰는 임시 로스터 제공자. 실제 데이터 시스템이 생기면
    /// 대체된다.
    /// - 용병: 캐릭터 카탈로그에서 직업별 테이블 순서 앞 2명으로 시작하고, 고용할 때마다 한 명씩 늘어난다(Docs/기획/53번 §3.2,
    ///   설계 54번 §4.2). 캐릭터마다 스탯·이름 행이 따로 있다.
    /// - 마차·시설: 기존대로 카테고리당 5개 고정(기획 11번 §3) - 획득 경로는 다른 시설 기획 몫이다.
    /// 로스터 목록은 소비자(정비창 팔레트)가 열릴 때마다 다시 읽으므로, 고용으로 목록이 늘어나도 별도 통지가 필요 없다. 고용 화면만
    /// 변경 이벤트(OnHiredChanged)를 구독한다.
    /// </summary>
    public class PlaceholderCaravanRosterProvider : MonoBehaviour, ICaravanRosterProvider, IHiredCharacterRoster, IMercenaryClassIconReader, IManagedComponent
    {
        // 기획 11번 §2 확정값 - 마차·시설 카테고리당 5개.
        private const int InstancesPerCategory = 5;
        // 기획 53번 §3.2 확정값 - 직업당 시작 보유 인원(테이블 순서 앞에서부터).
        private const int StartingCharactersPerClass = 2;

        [SerializeField] private Sprite warriorIcon;
        [SerializeField] private Sprite archerIcon;
        [SerializeField] private Sprite shieldBearerIcon;
        [SerializeField] private Sprite wagonIcon;
        [SerializeField] private Sprite facilityIcon;

        private readonly List<IFormationUnit> roster = new();
        private readonly HashSet<string> hiredCharacterIds = new();
        private readonly Dictionary<string, int> hiredCountByClass = new();
        private ICharacterCatalogReader catalog;

        public event Action OnHiredChanged;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ICaravanRosterProvider>(this);
            registrar.Register<IHiredCharacterRoster>(this);
            registrar.Register<IMercenaryClassIconReader>(this);
        }

        // registrar가 null이면(마차 수만 필요한 EditMode 테스트) 카탈로그 없이 마차·시설만 채운다.
        public void ResolveDependencies(IDependencyResolver registrar)
        {
            roster.Clear();
            hiredCharacterIds.Clear();
            hiredCountByClass.Clear();

            catalog = null;
            if (registrar != null && !registrar.TryResolve(out catalog))
            {
                Debug.LogWarning($"{nameof(PlaceholderCaravanRosterProvider)}: {nameof(ICharacterCatalogReader)}가 없어 용병 없이 시작한다(Tools > Game > Build Bootstrap Scene).");
            }

            AddStartingCharacters();
            AddFormationInstances("wagon", "마차", wagonIcon, FormationUnitKind.Wagon);
            AddFormationInstances("facility", "시설", facilityIcon, FormationUnitKind.Facility);
        }

        public IReadOnlyList<IFormationUnit> GetRoster() => roster;

        public bool IsHired(string characterId) => hiredCharacterIds.Contains(characterId);

        public int CountHiredOfClass(string mercenaryClass) => hiredCountByClass.TryGetValue(mercenaryClass, out var count) ? count : 0;

        public bool TryAddHired(string characterId)
        {
            if (!TryAddCharacter(characterId)) return false;

            OnHiredChanged?.Invoke();
            return true;
        }

        public Sprite GetClassIcon(string mercenaryClass) => mercenaryClass switch
        {
            "Warrior" => warriorIcon,
            "Archer" => archerIcon,
            "ShieldBearer" => shieldBearerIcon,
            _ => null,
        };

        private void AddStartingCharacters()
        {
            if (catalog == null) return;

            foreach (var profile in catalog.All)
            {
                if (CountHiredOfClass(profile.MercenaryClass) >= StartingCharactersPerClass) continue;
                TryAddCharacter(profile.CharacterId);
            }
        }

        // 용병은 직업끼리 모여 있어야 정비창 팔레트 순서(직업별 1줄)가 테이블 순서와 맞는다 - 같은 직업의 마지막 용병 뒤에 끼워 넣는다.
        private bool TryAddCharacter(string characterId)
        {
            if (catalog == null || hiredCharacterIds.Contains(characterId)) return false;
            if (!catalog.TryGet(characterId, out var profile)) return false;

            var unit = new PlaceholderMercenaryUnit(profile.CharacterId, profile.ClassLabel, GetClassIcon(profile.MercenaryClass), profile.MercenaryClass);
            roster.Insert(FindInsertIndex(profile.MercenaryClass), unit);
            hiredCharacterIds.Add(characterId);
            hiredCountByClass[profile.MercenaryClass] = CountHiredOfClass(profile.MercenaryClass) + 1;
            return true;
        }

        private int FindInsertIndex(string mercenaryClass)
        {
            var lastSameClass = -1;
            var firstNonMercenary = roster.Count;
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster[i] is IMercenaryUnit mercenary)
                {
                    if (mercenary.Class == mercenaryClass) lastSameClass = i;
                }
                else if (firstNonMercenary == roster.Count)
                {
                    firstNonMercenary = i;
                }
            }
            return lastSameClass >= 0 ? lastSameClass + 1 : firstNonMercenary;
        }

        private void AddFormationInstances(string idPrefix, string displayName, Sprite icon, FormationUnitKind kind)
        {
            for (var i = 1; i <= InstancesPerCategory; i++)
            {
                roster.Add(new PlaceholderFormationUnit($"{idPrefix}-{i}", displayName, icon, kind));
            }
        }
    }
}
