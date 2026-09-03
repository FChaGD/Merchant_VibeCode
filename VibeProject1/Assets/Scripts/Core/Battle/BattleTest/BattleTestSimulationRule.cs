using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 IBattleResultRule - LiveBattleSimulationRule(실제 게임)과 전투 수치/규칙
    /// (BattleCharacterUnit, PartyMorale, MoraleWaveCoordinator, Frontline/RangedSurround 조율자,
    /// 전술 해석 체계)은 전부 동일 클래스를 그대로 재사용하지만, "누가 언제 무엇으로 전투를
    /// 구성하는가"만 다르다:
    /// - IFormationRepository/ICaravanRosterProvider 대신 BattleTestAllyRoster/BattleTestEnemyRoster를
    ///   읽는다(그리드 슬롯이 아니라 자유 드래그로 배치된 월드 좌표 목록).
    /// - 보호 목표(Wagon/Facility)는 이 씬의 검증 범위 밖이라 항상 빈 목록이다.
    /// - PartyMorale/MoraleWaveCoordinator/Frontline·RangedSurround 조율자를 지역 변수가 아니라
    ///   필드로 캐싱해, Evaluate() 종료 후에도(=전투 진행 중에도) SpawnAlly/SpawnEnemy가 같은
    ///   인스턴스를 재사용할 수 있게 한다 - 안 그러면 전투 중 추가된 유닛이 이미 싸우던 유닛과 다른
    ///   사기/파동 시스템을 갖게 된다.
    /// - IStoppableBattleSimulation.Pause()/IResettableBattleSimulation.ResetToSetup()으로 정지/리셋을
    ///   지원한다(실제 게임의 IPausableBattleSimulation은 재개만 가능해 그대로 못 씀).
    /// </summary>
    public class BattleTestSimulationRule : MonoBehaviour,
        IBattleResultRule, IRequiresTacticsReader, IBattleSimulationEvents,
        IPausableBattleSimulation, IStoppableBattleSimulation, IResettableBattleSimulation, ILiveUnitSpawner
    {
        [SerializeField] private MercenaryRoleGroupMapAsset roleGroupMap;

        // 엑셀 임포트 결과 테이블(Docs/설계/17번 §6) - LiveBattleSimulationRule과 같은 배선 전례.
        // enemyEncounterCompositionTable은 이 씬이 실제로 쓰는 GetStatsForType에는 필요 없지만
        // TableEnemyTypeCompositionProvider 생성자 시그니처를 두 씬이 공유하므로 함께 배선한다.
        [SerializeField] private CharacterStatsTableAsset characterStatsTable;
        [SerializeField] private EnemyStatsTableAsset enemyStatsTable;
        [SerializeField] private EnemyEncounterCompositionTableAsset enemyEncounterCompositionTable;

        // 순수 C# 객체라 UnityEngine.Object가 아니다(SerializedObject로 연결 불가) - 인스톨러가 아니라
        // 이 컴포넌트가 직접 만들어 소유하고, 형제 컴포넌트(팔레트/기즈모/적 구성 패널)는 아래 공개
        // 프로퍼티로 GetComponent&lt;BattleTestSimulationRule&gt;() 경유해 접근한다.
        private readonly BattleTestFieldLayout fieldLayout = new();
        private readonly BattleTestAllyRoster allyRoster = new();
        private readonly BattleTestEnemyRoster enemyRoster = new();
        private readonly BattleTestSpawnPointReservations spawnPointReservations = new();
        // 예약이 로스터로 변환될 때 생긴 엔트리 Id들을 기억한다(한 스폰 포인트에서 타입별로 여러
        // 마리가 나올 수 있어 1:N) - Evaluate()가 다시 불릴 때마다(리셋 후 재시작 포함) 지우고 최신
        // 예약으로 다시 채워야 중복 추가되지 않는다.
        private readonly List<int> reservationEntryIds = new();

        public BattleTestFieldLayout FieldLayout => fieldLayout;
        public BattleTestAllyRoster AllyRoster => allyRoster;
        public BattleTestEnemyRoster EnemyRoster => enemyRoster;
        public BattleTestSpawnPointReservations SpawnPointReservations => spawnPointReservations;

        private IBattleUnitStatProvider statProvider;
        private TableEnemyTypeCompositionProvider enemyProvider;
        private readonly IDamageFormula damageFormula = new PlaceholderDamageFormula();
        private readonly IUnitSpatialQuery spatialQuery = new LinearScanUnitSpatialQuery();

        private void Awake()
        {
            statProvider = new TableBattleUnitStatProvider(characterStatsTable);
            enemyProvider = new TableEnemyTypeCompositionProvider(enemyStatsTable, enemyEncounterCompositionTable);
        }

        private ITacticsReader tacticsReader;
        private IUnitTacticsProfileResolver tacticsProfileResolver;

        private BattleSimulationLoop simulation;
        private Action<BattleResult> onResult;
        private bool resultReported;
        private bool paused;

        // 전투 도중 SpawnAlly/SpawnEnemy가 재사용해야 하는, BuildSimulation() 시점에만 지역 변수로
        // 살았던 값들을 필드로 승격한 것 - LiveBattleSimulationRule과 갈라지는 핵심 지점.
        private PartyMorale allyMorale;
        private PartyMorale enemyMorale;
        private MoraleWaveCoordinator allyWaveCoordinator;
        private MoraleWaveCoordinator enemyWaveCoordinator;
        private FrontlineFormationCoordinator frontlineCoordinator;
        private RangedSurroundCoordinator rangedSurroundCoordinator;
        private float fleeTravelDistance;
        private float standardActivityRadius;
        private float fieldRadius;

        public event Action<BattleSimulationLoop> OnSimulationBuilt;
        public event Action OnReset;
        // 팔레트 드래그로 유닛이 하나 추가될 때마다 발행한다 - BattleViewPresenter는 OnSimulationBuilt
        // (Evaluate() 1회)에만 반응해 그 이후 RegisterAdditionalUnit으로 늘어난 유닛은 뷰가 안 생긴다.
        // 세팅 단계(전투 시작 전)에도 드롭 위치에 미리보기가 보여야 하므로, 실행 중 여부와 무관하게
        // 매번 발행한다 - BattleTestController가 구독해 유닛 뷰를 직접 스폰한다. 세 번째 인자(로스터
        // Entry.Id)는 세팅 단계 유닛에만 의미가 있다 - BattleTestUnitClickTarget이 "배치 취소"/스탯
        // 조절 대상을 식별하는 데 쓴다(전투 중 실시간 추가 유닛은 Id가 있어도 클릭 대상으로 삼지
        // 않는다 - BattleTestController.HandleUnitAdded가 IsRunning으로 분기).
        public event Action<IBattleCombatant, bool, int> OnUnitAdded;
        // 적 구성 편집 패널의 "적용"이 로스터를 통째로 다시 채우기 직전에 발행한다 - 컨트롤러가
        // enemyContainer(아군 쪽은 그대로 둔 채)만 비우는 신호로 쓴다(OnReset과 달리 아군까지 지우면
        // 안 된다).
        public event Action OnEnemyRosterCleared;

        private readonly IEncounterSpawnPointSelector enemySetupSpawnSelector = new UniformRandomSpawnPointSelector();

        public bool IsRunning => simulation != null;
        // 승패가 이미 갈렸는데 리셋 없이 "시작"을 다시 누른 경우를 위한 신호 - Update()가
        // resultReported==true인 동안 계속 틱을 건너뛰므로, 컨트롤러가 이 값을 보고 재개 대신
        // 새로 시작해야 함을 판단한다.
        public bool IsFinished => resultReported;
        public float AllyMoraleValue => allyMorale?.CurrentValue ?? 0f;
        public float EnemyMoraleValue => enemyMorale?.CurrentValue ?? 0f;

        public void SetTacticsReader(ITacticsReader reader) => tacticsReader = reader;

        public void Evaluate(Action<BattleResult> onResult)
        {
            this.onResult = onResult;
            resultReported = false;
            paused = true;
            SyncReservationsIntoRoster();
            simulation = BuildSimulation();
            OnSimulationBuilt?.Invoke(simulation);
        }

        // 스폰 포인트 예약(요구사항: 전투 시작 전 예약)을 실제 로스터 엔트리로 변환한다 - 전투 시작
        // 순간에만 호출되므로 그 전까지는 필드에 유닛이 보이지 않는다. 이전 변환분을 먼저 지우고
        // 다시 채우는 이유는 재시작(리셋 후 다시 시작)마다 같은 예약이 중복 추가되지 않게 하기
        // 위함이자, 그 사이 패널에서 바뀐 예약을 그대로 반영하기 위함이다.
        private void SyncReservationsIntoRoster()
        {
            foreach (var entryId in reservationEntryIds) enemyRoster.Remove(entryId);
            reservationEntryIds.Clear();

            foreach (var reservation in spawnPointReservations.All)
            {
                var position = fieldLayout.ComputeSpawnPoint(reservation.Key, fieldLayout.ColumnCount);
                var composition = reservation.Value;
                AddReservedBatch(EnemyType.Marauder, composition.Marauder, position);
                AddReservedBatch(EnemyType.Monster, composition.Monster, position);
                AddReservedBatch(EnemyType.Adversary, composition.Adversary, position);
            }
        }

        private void AddReservedBatch(EnemyType type, int count, Vector2 position)
        {
            for (var i = 0; i < count; i++)
            {
                var entry = enemyRoster.Add(type, position);
                reservationEntryIds.Add(entry.Id);
            }
        }

        public void ResumeSimulation() => paused = false;

        public void Pause() => paused = true;

        // 리셋은 "세팅 상태로 복귀"다(계획 확인 사항) - 로스터(BattleTestAllyRoster/EnemyRoster)는
        // 지우지 않고 그대로 두므로, 컨트롤러가 OnReset으로 뷰를 비운 직후 로스터 내용을 미리보기로
        // 다시 그려줘야 "배치했던 유닛이 그대로 보인다"는 기대와 맞는다 - 안 그러면 데이터는
        // 남아있는데 화면만 텅 비어 보이는 버그가 된다(실전투 확인됨).
        public void ResetToSetup()
        {
            simulation = null;
            resultReported = false;
            paused = false;
            OnReset?.Invoke();
            ReplayRosterAsPreview();
        }

        private void ReplayRosterAsPreview()
        {
            foreach (var entry in allyRoster.Entries)
            {
                OnUnitAdded?.Invoke(BuildPreviewAlly(entry), true, entry.Id);
            }
            foreach (var entry in enemyRoster.Entries)
            {
                OnUnitAdded?.Invoke(BuildPreviewEnemy(entry), false, entry.Id);
            }
        }

        public void SpawnAlly(MercenaryClass unitClass, Vector2 worldPosition)
        {
            var entry = allyRoster.Add(unitClass, worldPosition);

            if (IsRunning)
            {
                var stats = statProvider.GetStats(unitClass);
                var tacticsBehaviors = BuildTacticsBehaviors(unitClass, worldPosition);
                var unit = new BattleCharacterUnit(worldPosition, isAlly: true, stats, damageFormula, allyMorale, allyWaveCoordinator, spatialQuery, fleeTravelDistance, tacticsBehaviors);
                simulation.RegisterAdditionalUnit(unit, isAlly: true);
                OnUnitAdded?.Invoke(unit, true, entry.Id);
            }
            else
            {
                OnUnitAdded?.Invoke(BuildPreviewAlly(entry), true, entry.Id);
            }
        }

        public void SpawnEnemy(EnemyType enemyType, Vector2 worldPosition)
        {
            var entry = enemyRoster.Add(enemyType, worldPosition);

            if (IsRunning)
            {
                var stats = enemyProvider.GetStatsForType(enemyType);
                var unit = new BattleCharacterUnit(worldPosition, isAlly: false, stats, damageFormula, enemyMorale, enemyWaveCoordinator, spatialQuery, fleeTravelDistance, icon: BattlePlaceholderSprite.ForEnemyType(enemyType));
                simulation.RegisterAdditionalUnit(unit, isAlly: false);
                OnUnitAdded?.Invoke(unit, false, entry.Id);
            }
            else
            {
                OnUnitAdded?.Invoke(BuildPreviewEnemy(entry), false, entry.Id);
            }
        }

        // 적 구성 편집 패널의 "적용" 버튼이 호출한다(요구사항 #6) - 로스터를 통째로 비우고 타입별
        // 개수만큼 다시 채운다. 로스터를 직접 Clear/Add하지 않고 이 메서드를 거치는 이유 - 그렇게
        // 하면 OnEnemyRosterCleared/OnUnitAdded가 안 불려 미리보기 뷰가 갱신되지 않는 버그가 된다
        // (팔레트 드래그/리셋과 같은 원인, 실전투 확인됨).
        public void ReplaceEnemyComposition(int marauderCount, int monsterCount, int adversaryCount)
        {
            enemyRoster.Clear();
            OnEnemyRosterCleared?.Invoke();

            AddEnemyBatch(EnemyType.Marauder, marauderCount);
            AddEnemyBatch(EnemyType.Monster, monsterCount);
            AddEnemyBatch(EnemyType.Adversary, adversaryCount);
        }

        private void AddEnemyBatch(EnemyType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var position = fieldLayout.ComputeSpawnPoint(enemySetupSpawnSelector.SelectSpawnPointIndex(), fieldLayout.ColumnCount);
                var entry = enemyRoster.Add(type, position);
                OnUnitAdded?.Invoke(BuildPreviewEnemy(entry), false, entry.Id);
            }
        }

        // 배치 취소(요구사항) - 세팅 단계 로스터에서만 의미가 있다. 뷰 파괴는 클릭 당시 참조를 이미
        // 들고 있는 BattleTestUnitInfoPanelView가 직접 처리한다(왕복 이벤트 없이 단순하게).
        public void RemoveAlly(int entryId) => allyRoster.Remove(entryId);

        public void RemoveEnemy(int entryId) => enemyRoster.Remove(entryId);

        // 유닛 정보 패널의 "적용" 버튼이 호출한다 - 다음 BuildSimulation()/미리보기 재생성부터
        // 반영된다(BattleCharacterUnit.stats는 생성 후 불변이라 즉시 살아있는 인스턴스를 바꿀 수는
        // 없다 - 세팅 단계 유닛만 클릭 가능하게 범위를 좁힌 이유이기도 하다).
        public void SetAllyStatsOverride(int entryId, BattleUnitStats stats)
        {
            if (allyRoster.TryGet(entryId, out var entry)) entry.StatsOverride = stats;
        }

        public void SetEnemyStatsOverride(int entryId, BattleUnitStats stats)
        {
            if (enemyRoster.TryGet(entryId, out var entry)) entry.StatsOverride = stats;
        }

        public BattleUnitStats GetAllyDefaultStats(MercenaryClass unitClass) => statProvider.GetStats(unitClass);

        public BattleUnitStats GetEnemyDefaultStats(EnemyType type) => enemyProvider.GetStatsForType(type);

        // 세팅 단계(전투 시작 전/리셋 후) 미리보기 전용 - 절대 Tick되지 않으므로(BattleSimulationLoop에
        // 등록되지 않음) PartyMorale/MoraleWaveCoordinator/fleeTravelDistance는 새로 만든 더미
        // 인스턴스를 써도 무해하다. 실제 전투용 유닛은 BuildAllies/BuildEnemies가 별도로 만든다.
        private BattleCharacterUnit BuildPreviewAlly(BattleTestAllyRoster.Entry entry)
        {
            var stats = entry.StatsOverride ?? statProvider.GetStats(entry.Class);
            return new BattleCharacterUnit(entry.Position, isAlly: true, stats, damageFormula, new PartyMorale(), new MoraleWaveCoordinator(0f), spatialQuery, 0f);
        }

        private BattleCharacterUnit BuildPreviewEnemy(BattleTestEnemyRoster.Entry entry)
        {
            var stats = entry.StatsOverride ?? enemyProvider.GetStatsForType(entry.Type);
            return new BattleCharacterUnit(entry.Position, isAlly: false, stats, damageFormula, new PartyMorale(), new MoraleWaveCoordinator(0f), spatialQuery, 0f, icon: BattlePlaceholderSprite.ForEnemyType(entry.Type));
        }

        private void Update()
        {
            if (simulation == null || resultReported || paused) return;

            simulation.Tick(Time.deltaTime);

            if (simulation.IsEnemyWiped) Report(BattleOutcome.Victory);
            else if (simulation.IsAllyWiped) Report(BattleOutcome.Defeat);
        }

        private void Report(BattleOutcome outcome)
        {
            resultReported = true;
            onResult(new BattleResult(outcome));
        }

        private BattleSimulationLoop BuildSimulation()
        {
            var columnCount = fieldLayout.ColumnCount;
            fleeTravelDistance = fieldLayout.ComputeFleeTravelDistance(columnCount);
            standardActivityRadius = fieldLayout.ComputeStandardActivityRadius(columnCount);
            fieldRadius = fieldLayout.ComputeFieldRadius(columnCount);
            var spawnRadius = fieldLayout.ComputeSpawnRadius(columnCount);

            allyMorale = new PartyMorale();
            enemyMorale = new PartyMorale();
            allyWaveCoordinator = new MoraleWaveCoordinator(fieldRadius);
            enemyWaveCoordinator = new MoraleWaveCoordinator(fieldRadius);

            tacticsProfileResolver = tacticsReader != null
                ? new UnitTacticsProfileResolver(tacticsReader, roleGroupMap)
                : null;
            var partyPursuitPreset = tacticsReader?.GetPartySettings().Pursuit ?? PursuitPreset.OffensiveJudgment;
            frontlineCoordinator = new FrontlineFormationCoordinator(standardActivityRadius, partyPursuitPreset);
            rangedSurroundCoordinator = new RangedSurroundCoordinator(standardActivityRadius, frontlineCoordinator);

            var allies = BuildAllies();
            var enemies = BuildEnemies();
            var protectedUnits = new List<IDamageable>(); // 이 씬의 검증 범위 밖(보호 목표 없음).

            return new BattleSimulationLoop(allies, enemies, protectedUnits, fieldRadius, spawnRadius, frontlineCoordinator, rangedSurroundCoordinator, allyWaveCoordinator, enemyWaveCoordinator);
        }

        private List<IBattleCombatant> BuildAllies()
        {
            var allies = new List<IBattleCombatant>(allyRoster.Entries.Count);
            foreach (var entry in allyRoster.Entries)
            {
                var stats = entry.StatsOverride ?? statProvider.GetStats(entry.Class);
                var tacticsBehaviors = BuildTacticsBehaviors(entry.Class, entry.Position);
                allies.Add(new BattleCharacterUnit(entry.Position, isAlly: true, stats, damageFormula, allyMorale, allyWaveCoordinator, spatialQuery, fleeTravelDistance, tacticsBehaviors));
            }
            return allies;
        }

        private List<IBattleCombatant> BuildEnemies()
        {
            var enemies = new List<IBattleCombatant>(enemyRoster.Entries.Count);
            foreach (var entry in enemyRoster.Entries)
            {
                var stats = entry.StatsOverride ?? enemyProvider.GetStatsForType(entry.Type);
                enemies.Add(new BattleCharacterUnit(entry.Position, isAlly: false, stats, damageFormula, enemyMorale, enemyWaveCoordinator, spatialQuery, fleeTravelDistance, icon: BattlePlaceholderSprite.ForEnemyType(entry.Type)));
            }
            return enemies;
        }

        // 배치 슬롯 좌표 대신 드롭 좌표를 그대로 HomePosition으로 쓴다 - 자유 배치라 "슬롯"이라는
        // 개념이 없을 뿐, 방향성 지시가 참조하는 의미(그 유닛이 원래 있어야 할 자리)는 동일하다.
        private UnitTacticsBehaviors BuildTacticsBehaviors(MercenaryClass unitClass, Vector2 homePosition)
        {
            if (tacticsProfileResolver == null) return null;

            var profile = tacticsProfileResolver.Resolve(unitClass, homePosition);
            return UnitTacticsBehaviorsFactory.Build(profile, standardActivityRadius, fieldRadius, spatialQuery, frontlineCoordinator, rangedSurroundCoordinator);
        }
    }
}
