using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 관리 데이터 시스템이 아직 없어, 배치 UI 팔레트·전투·시설 화면이 함께 쓰는 임시 로스터 제공자. 실제 데이터 시스템이 생기면
    /// 대체된다.
    /// - 용병: 캐릭터 카탈로그에서 직업별 테이블 순서 앞 2명으로 시작하고, 고용할 때마다 늘어난다(Docs/기획/53번, 설계 54번 §4.2).
    /// - 마차·시설: 마차·시설 카탈로그에서 종류별 첫 행 1개로 시작하고, 마구간 구매로 늘어난다(기획 55번, 설계 56번 §4).
    ///   전투에서 파괴된 마차는 정산이 보유 목록에서 제거한다(기획 77번 §3-8, 설계 79번 §5.2).
    ///   개체 Id는 장부(OwnedCaravanAssetRegistry)가 발급한다 - 같은 종류를 여러 대 가질 수 있다(기획 80번 §3-3, 설계 81번 §5.1).
    /// 로스터 목록은 소비자(정비창 팔레트)가 열릴 때마다 다시 읽으므로 추가 통지가 필요 없다 - 시설 화면만 변경 이벤트를 구독한다.
    /// 목록은 항상 "용병(캐릭터 테이블 순) → 마차 → 시설(각각 보유 순)"으로 다시 채운다. 팔레트가 첫 등장 순서로 줄을 만들기
    /// 때문에, 추가 위치를 따로 계산하지 않고 테이블 순서로 재구성하는 편이 순서가 어긋날 여지가 없다. 같은 List 인스턴스를 비우고
    /// 다시 채워 이미 참조를 쥔 소비자도 최신 목록을 본다.
    /// </summary>
    public class PlaceholderCaravanRosterProvider : MonoBehaviour, ICaravanRosterProvider, IHiredCharacterRoster, IMercenaryClassIconReader, IOwnedCaravanAssetRoster, IOwnedCaravanAssetRemover, ICaravanAssetIconReader, IManagedComponent
    {
        // 기획 53번 §3.2 확정값 - 직업당 시작 보유 인원(테이블 순서 앞에서부터).
        private const int StartingCharactersPerClass = 2;
        // 기획 55번 §3 확정값 - 마차·시설 종류별 시작 보유 수(테이블 순서 앞에서부터).
        private const int StartingAssetsPerKind = 1;
        // 로스터 목록의 마차·시설 순서(카탈로그 목록 순서와 같다).
        private static readonly FormationUnitKind[] AssetKindOrder = { FormationUnitKind.Wagon, FormationUnitKind.Facility };

        [SerializeField] private Sprite warriorIcon;
        [SerializeField] private Sprite archerIcon;
        [SerializeField] private Sprite shieldBearerIcon;
        [SerializeField] private Sprite wagonIcon;
        [SerializeField] private Sprite facilityIcon;

        private readonly List<IFormationUnit> roster = new();
        private readonly Dictionary<string, IFormationUnit> ownedById = new();
        private readonly Dictionary<string, int> hiredCountByClass = new();
        // 마차·시설 개체 장부 - 발급 Id·종류 매핑·보유 순서·번호(설계 81번 §5.1). ownedById(Dictionary)에는 순서가 없어 따로 둔다.
        private readonly OwnedCaravanAssetRegistry assetRegistry = new();
        private ICharacterCatalogReader characterCatalog;
        private ICaravanAssetCatalogReader assetCatalog;

        public event Action OnHiredChanged;
        public event Action OnOwnedChanged;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ICaravanRosterProvider>(this);
            registrar.Register<IHiredCharacterRoster>(this);
            registrar.Register<IMercenaryClassIconReader>(this);
            registrar.Register<IOwnedCaravanAssetRoster>(this);
            registrar.Register<IOwnedCaravanAssetReader>(this);
            registrar.Register<IOwnedCaravanAssetRemover>(this);
            registrar.Register<ICaravanAssetIconReader>(this);
        }

        // registrar가 null이면(EditMode 테스트) 카탈로그 없이 빈 로스터로 시작한다.
        public void ResolveDependencies(IDependencyResolver registrar)
        {
            ownedById.Clear();
            hiredCountByClass.Clear();
            assetRegistry.Clear();

            characterCatalog = null;
            assetCatalog = null;
            if (registrar != null)
            {
                if (!registrar.TryResolve(out characterCatalog))
                {
                    Debug.LogWarning($"{nameof(PlaceholderCaravanRosterProvider)}: {nameof(ICharacterCatalogReader)}가 없어 용병 없이 시작한다(Tools > Game > Build Bootstrap Scene).");
                }
                if (!registrar.TryResolve(out assetCatalog))
                {
                    Debug.LogWarning($"{nameof(PlaceholderCaravanRosterProvider)}: {nameof(ICaravanAssetCatalogReader)}가 없어 마차·시설 없이 시작한다(Tools > Game > Build Bootstrap Scene).");
                }
            }

            AddStartingCharacters();
            AddStartingAssets();
            RebuildRoster();
            // 이 시점 이전에 먼저 해결된 구독자(상단 물류품 저장소 등)도 시작 보유분을 받도록 알린다 - DI 해결 순서와 무관하게
            // 동기화되게 하기 위함이다(설계 64번 §5).
            OnOwnedChanged?.Invoke();
        }

        public IReadOnlyList<IFormationUnit> GetRoster() => roster;

        public bool IsHired(string characterId) => characterCatalog != null && characterCatalog.TryGet(characterId, out _) && ownedById.ContainsKey(characterId);

        public int CountHiredOfClass(string mercenaryClass) => hiredCountByClass.TryGetValue(mercenaryClass, out var count) ? count : 0;

        public bool TryAddHired(string characterId)
        {
            if (!TryOwnCharacter(characterId)) return false;

            RebuildRoster();
            OnHiredChanged?.Invoke();
            return true;
        }

        public int CountOwnedOfKind(FormationUnitKind kind) => assetRegistry.Count(kind);

        public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => assetRegistry.GetIds(kind);

        public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset)
        {
            asset = default;
            if (assetCatalog == null || !assetRegistry.TryGetKindId(instanceId, out var kindId) || !assetCatalog.TryGet(kindId, out var profile)) return false;
            asset = new OwnedCaravanAsset(instanceId, profile, assetRegistry.NumberOf(instanceId));
            return true;
        }

        public bool TryAddOwned(string kindId, out string instanceId)
        {
            if (!TryOwnAsset(kindId, out instanceId)) return false;

            RebuildRoster();
            OnOwnedChanged?.Invoke();
            return true;
        }

        // 마차·시설만 제거한다 - 용병 해고는 이 경로의 범위가 아니다. 보유 목록 변경 이벤트로 상단 물류품 저장소가 섹션을 함께 제거한다.
        public bool TryRemoveOwned(string id)
        {
            if (assetCatalog == null || !assetRegistry.Remove(id)) return false;

            ownedById.Remove(id);
            RebuildRoster();
            OnOwnedChanged?.Invoke();
            return true;
        }

        public Sprite GetClassIcon(string mercenaryClass) => mercenaryClass switch
        {
            "Warrior" => warriorIcon,
            "Archer" => archerIcon,
            "ShieldBearer" => shieldBearerIcon,
            _ => null,
        };

        public Sprite GetKindIcon(FormationUnitKind kind) => kind switch
        {
            FormationUnitKind.Wagon => wagonIcon,
            FormationUnitKind.Facility => facilityIcon,
            _ => null,
        };

        private void AddStartingCharacters()
        {
            if (characterCatalog == null) return;

            foreach (var profile in characterCatalog.All)
            {
                if (CountHiredOfClass(profile.MercenaryClass) >= StartingCharactersPerClass) continue;
                TryOwnCharacter(profile.CharacterId);
            }
        }

        private void AddStartingAssets()
        {
            if (assetCatalog == null) return;

            foreach (var profile in assetCatalog.All)
            {
                if (CountOwnedOfKind(profile.Kind) >= StartingAssetsPerKind) continue;
                TryOwnAsset(profile.Id, out _);
            }
        }

        private bool TryOwnCharacter(string characterId)
        {
            if (characterCatalog == null || ownedById.ContainsKey(characterId)) return false;
            if (!characterCatalog.TryGet(characterId, out var profile)) return false;

            ownedById[characterId] = new PlaceholderMercenaryUnit(profile.CharacterId, profile.ClassLabel, GetClassIcon(profile.MercenaryClass), profile.MercenaryClass);
            hiredCountByClass[profile.MercenaryClass] = CountHiredOfClass(profile.MercenaryClass) + 1;
            return true;
        }

        // 개체 Id는 장부가 발급한다(설계 81번 §5.1). 표시명은 종류명 - 팔레트 카테고리 이름으로 쓰인다. 개체 이름은 정보 패널이 InstanceName으로 읽는다.
        private bool TryOwnAsset(string kindId, out string instanceId)
        {
            instanceId = null;
            if (assetCatalog == null || kindId == null || !assetCatalog.TryGet(kindId, out var profile)) return false;

            var id = assetRegistry.Add(kindId, profile.Kind);
            ownedById[id] = new CaravanAssetFormationUnit(id, profile.KindLabel, GetKindIcon(profile.Kind), profile.Kind, profile.AreaShape,
                () => TryGetOwned(id, out var asset) ? OwnedCaravanAssetNames.Format(asset) : profile.Name);
            instanceId = id;
            return true;
        }

        private void RebuildRoster()
        {
            roster.Clear();
            if (characterCatalog != null)
            {
                foreach (var profile in characterCatalog.All)
                {
                    if (ownedById.TryGetValue(profile.CharacterId, out var unit)) roster.Add(unit);
                }
            }
            foreach (var kind in AssetKindOrder)
            {
                foreach (var id in assetRegistry.GetIds(kind)) roster.Add(ownedById[id]);
            }
        }
    }
}
