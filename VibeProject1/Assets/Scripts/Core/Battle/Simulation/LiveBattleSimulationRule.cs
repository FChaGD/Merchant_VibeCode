using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PlaceholderBattleResultRule을 대체하는 실제 전투 시뮬레이션. BattleManager가
    /// GetComponent&lt;IBattleResultRule&gt;()로 조회하므로 이 컴포넌트를 붙이는 것만으로 교체된다
    /// (BattleManager 무변경). 승패 조건: 적 전멸(사망+도주)=Victory, 아군 전멸(사망+도주)
    /// 또는 보호 목표 파괴=Defeat.
    /// </summary>
    public class LiveBattleSimulationRule : MonoBehaviour, IBattleResultRule, IRequiresFormationReader, IRequiresCaravanRoster, IRequiresTacticsReader, IRequiresUnitConditionRepository, IRequiresFieldFormationActivityRepository, IBattleSimulationEvents, IPausableBattleSimulation
    {
        // 직업→역할군 매핑 - 실제 데이터(직업별 항목)는 에디터에서 에셋을 만들어 채운다
        // (Docs/설계/12번 §2.1). 비어있으면 UnitTacticsProfileResolver가 경고 후 기본값으로 대체한다.
        [SerializeField] private MercenaryRoleGroupMapAsset roleGroupMap;

        // 엑셀 임포트 결과 테이블(Docs/설계/17번 §6) - roleGroupMap과 같은 배선 전례(소비자별
        // SerializeField + 인스톨러가 채움). statProvider/enemyProvider는 이 값들을 참조해야 해서
        // 더 이상 인라인 필드 초기화(readonly)로 만들 수 없어 Awake()에서 대입한다.
        [SerializeField] private CharacterStatsTableAsset characterStatsTable;
        [SerializeField] private EnemyStatsTableAsset enemyStatsTable;
        [SerializeField] private EnemyEncounterCompositionTableAsset enemyEncounterCompositionTable;

        private IBattleUnitStatProvider statProvider;
        private readonly IEncounterSpawnPointSelector spawnSelector = new UniformRandomSpawnPointSelector();
        private IEnemyCompositionProvider enemyProvider;
        // 아군 좌표 변환과 스폰/반지름 계산은 서로 다른 인터페이스지만 구현은 하나 - 내부 헬퍼
        // 공유 때문에 클래스까지 나누지 않았다(BattleFieldLayout, Docs/설계/12번 §5.2).
        private readonly BattleFieldLayout sharedFieldLayout = new();
        private IAllyPositionLayout FieldPositionLayout => sharedFieldLayout;
        private IBattleFieldGeometry FieldGeometry => sharedFieldLayout;
        private readonly IDamageFormula damageFormula = new PlaceholderDamageFormula();
        private readonly IUnitSpatialQuery spatialQuery = new LinearScanUnitSpatialQuery();

        private IFormationReader formationReader;
        private ICaravanRosterProvider rosterProvider;
        private ITacticsReader tacticsReader;
        private IUnitConditionRepository unitConditionRepository;
        private IFieldFormationActivityRepository fieldActivityRepository;
        private BattleSimulationLoop simulation;
        private Action<BattleResult> onResult;
        private bool resultReported;
        // 이번 전투에 참여한 아군의 unitId - 전투 종료 시 각자 최종 HP를 unitConditionRepository에
        // 되돌려 쓰기 위해 BuildAllies()가 채워둔다(설계 15번 §3). BuildSimulation()마다 새로 채운다.
        private readonly List<(string unitId, IBattleCombatant unit)> allyUnitIds = new();

        // BuildSimulation()이 만든 진영 공용 협력 객체들 - 원래는 지역 변수였지만, 전투 중 Field 배치
        // 타이머가 완료돼 새 아군을 늦게 합류시킬 때(설계 25번 §6) BuildSimulation() 스코프 밖에서도
        // 같은 인스턴스를 재사용해야 해서 필드로 승격했다(BattleTestSimulationRule이 같은 이유로
        // 이미 필드 캐싱을 쓰고 있는 것과 동일한 패턴). Evaluate()마다(=전투 시작마다) 새로 채워진다.
        private PartyMorale midBattleAllyMorale;
        private MoraleWaveCoordinator midBattleAllyWaveCoordinator;
        private IUnitTacticsProfileResolver midBattleTacticsProfileResolver;
        private float midBattleStandardActivityRadius;
        private float midBattleFieldRadius;
        private float midBattleFleeTravelDistance;
        private FrontlineFormationCoordinator midBattleFrontlineCoordinator;
        private RangedSurroundCoordinator midBattleRangedSurroundCoordinator;

        // 화면(커튼)이 완전히 드러나기 전까지는 유닛 위치만 잡아두고 틱은 멈춰둔다(사용자 확정) -
        // 안 그러면 페이드 아웃 도중 반투명해진 커튼 너머로 이미 움직이는 전투가 비쳐 보인다.
        // Evaluate()가 매 전투 시작 시 다시 true로 세팅하고, ResumeSimulation()이 걷힘 완료 시
        // false로 풀어준다(FieldEncounterFlowCoordinator 참고).
        private bool paused;

        public event Action<BattleSimulationLoop> OnSimulationBuilt;
        public event Action<IBattleCombatant> OnAllySpawnedMidBattle;
        public event Action<IReadOnlyList<PendingReinforcementInfo>> OnPendingReinforcementsChanged;

        private void Awake()
        {
            statProvider = new TableBattleUnitStatProvider(characterStatsTable);
            enemyProvider = new TableEnemyTypeCompositionProvider(enemyStatsTable, enemyEncounterCompositionTable);
        }

        // BattleManager.ResolveDependencies가 IRequiresFormationReader/IRequiresCaravanRoster/
        // IRequiresTacticsReader로 캐스팅해 호출한다.
        public void SetFormationReader(IFormationReader reader) => formationReader = reader;
        public void SetCaravanRoster(ICaravanRosterProvider provider) => rosterProvider = provider;
        public void SetTacticsReader(ITacticsReader reader) => tacticsReader = reader;
        public void SetUnitConditionRepository(IUnitConditionRepository repository) => unitConditionRepository = repository;

        // BattleManager.ResolveDependencies가 IRequiresFieldFormationActivityRepository로 캐스팅해
        // 호출한다(설계 25번 §6.1). 구독은 여기서 한 번만 건다 - 이 세터 자체가 게임 세션당 1회만
        // 호출되는 배선 시점이라(다른 SetXxx들과 동일 자리) 중복 구독 걱정이 없다.
        public void SetFieldFormationActivityRepository(IFieldFormationActivityRepository repository)
        {
            fieldActivityRepository = repository;
            fieldActivityRepository.OnActivityCompleted += HandleFieldActivityCompleted;
        }

        public void Evaluate(Action<BattleResult> onResult)
        {
            this.onResult = onResult;
            resultReported = false;
            paused = true;
            simulation = BuildSimulation();
            OnSimulationBuilt?.Invoke(simulation);
            // 실제 캐릭터(OnSimulationBuilt)와 같은 시점에 유령도 배치한다(사용자 확정, 2026-09-05) -
            // Update()는 paused 동안 호출되지 않아(§ResumeSimulation 참고), 여기서 미리 한 번 알려주지
            // 않으면 커튼이 걷혀 실제 캐릭터가 이미 보이는 뒤에야 유령이 뒤늦게 나타나 어색해 보였다.
            NotifyPendingReinforcements();
        }

        // BattleManager.ResumeSimulation()이 IPausableBattleSimulation으로 캐스팅해 호출한다. 이
        // 시점이 곧 "전투 시작"이라(커튼이 완전히 걷힌 뒤) Field 배치 활동 중 Adding만 재개한다 -
        // Moving은 전투가 끝날 때까지 계속 일시정지 상태로 남는다(기획 20번 §3.3, 설계 25번 §8.2).
        public void ResumeSimulation()
        {
            paused = false;
            fieldActivityRepository?.ResumeAdding();
        }

        private void Update()
        {
            if (simulation == null || resultReported || paused) return;

            simulation.Tick(Time.deltaTime);
            NotifyPendingReinforcements();

            if (simulation.IsEnemyWiped) Report(BattleOutcome.Victory);
            else if (simulation.IsAllyWiped || simulation.IsProtectionTargetDestroyed) Report(BattleOutcome.Defeat);
        }

        // 전투 중(정비창 UI를 열 수 없는 상태)에도 "곧 합류할 아군"을 알 수 있게, 매 틱 진행 중인
        // Adding 활동 전부를 세계 좌표로 환산해 넘긴다(사용자 확정 2026-09-05, 기획 20번 §1 원안의
        // "정비창 UI 안에서만 표시"보다 넓은 범위 - 설계 25번 §6.3). 마차/시설은 애초에 전투 중
        // 합류를 지원하지 않으므로(BuildSingleAlly가 Character만 다룸, §6.1) 제외한다.
        private void NotifyPendingReinforcements()
        {
            if (fieldActivityRepository == null || formationReader == null || !formationReader.TryLoadCurrent(out var layout))
            {
                OnPendingReinforcementsChanged?.Invoke(Array.Empty<PendingReinforcementInfo>());
                return;
            }

            var pending = new List<PendingReinforcementInfo>();
            foreach (var activity in fieldActivityRepository.ActiveActivities)
            {
                if (activity.Kind != FormationActivityKind.Adding) continue;

                var rosterUnit = FindRosterUnit(activity.UnitId);
                if (rosterUnit == null || rosterUnit.Kind != FormationUnitKind.Character) continue;

                var column = activity.TargetSlotIndex % layout.ColumnCount;
                var row = activity.TargetSlotIndex / layout.ColumnCount;
                var position = FieldPositionLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
                pending.Add(new PendingReinforcementInfo(activity.UnitId, position, activity.RequiredSeconds - activity.ElapsedSeconds));
            }
            OnPendingReinforcementsChanged?.Invoke(pending);
        }

        private void Report(BattleOutcome outcome)
        {
            resultReported = true;
            SyncRosterConditionFromBattle();
            // 전투가 끝나는 순간 Adding도 다시 일시정지한다(기획 20번 §3.2 "전투 종료 시 타이머 재중단") -
            // 이후 승리/도주는 FieldEncounterFlowCoordinator.ShowResult가 ResumeAll()로, 패배(도주
            // 제외)는 ShowDefeatConsequence가 ForceCompleteAll()로 마무리한다(설계 25번 §8.3/§8.4).
            fieldActivityRepository?.PauseAll();
            // 전투 종료 즉시 유령 표시를 지운다 - 다음 전투 시작(Present())까지 화면에 남아있지 않게 한다.
            OnPendingReinforcementsChanged?.Invoke(Array.Empty<PendingReinforcementInfo>());
            onResult(new BattleResult(outcome));
        }

        // 전투 중 Field 배치 타이머가 완료됐을 때 호출된다(IFieldFormationActivityRepository.OnActivityCompleted
        // 구독, 설계 25번 §6.1). 전투 중이 아니거나(simulation==null/paused) Moving 활동이면 아무 것도
        // 하지 않는다 - 로스터/레이아웃 반영은 이미 저장소의 Complete()가 끝냈으므로, 다음 전투의
        // BuildAllies()가 자연히 그 슬롯을 읽어간다.
        private void HandleFieldActivityCompleted(FormationActivity activity)
        {
            if (simulation == null || paused || resultReported || activity.Kind != FormationActivityKind.Adding) return;
            if (formationReader == null || !formationReader.TryLoadCurrent(out var layout)) return;

            var newAlly = BuildSingleAlly(layout, activity.TargetSlotIndex);
            if (newAlly == null) return;

            simulation.RegisterAdditionalUnit(newAlly, isAlly: true);
            allyUnitIds.Add((activity.UnitId, newAlly));
            OnAllySpawnedMidBattle?.Invoke(newAlly);
        }

        // 전투가 끝난 시점(승/패 확정)의 아군 최종 상태를 로스터 저장소에 되돌려 쓴다(설계 15번 §3b).
        // 사망 여부는 IsAlive(도주/이탈 포함)가 아니라 CurrentHp<=0f로 직접 판정한다 - 도주 등은
        // 사망이 아니라 HP만 유지해야 한다(기획 13번 §5/§6).
        private void SyncRosterConditionFromBattle()
        {
            if (unitConditionRepository == null) return;

            foreach (var (unitId, unit) in allyUnitIds)
            {
                unitConditionRepository.ApplyBattleResult(unitId, unit.CurrentHp, died: unit.CurrentHp <= 0f);
            }
        }

        private BattleSimulationLoop BuildSimulation()
        {
            // 전투마다 새로 채운다(설계 15번 §3b) - 이전 전투의 매핑이 남아있으면 이번 전투 종료 시
            // 그 유닛들에게도 잘못된 결과를 되돌려 쓰게 된다.
            allyUnitIds.Clear();

            // hasLayout을 별도 변수로 옮겨 담으면 컴파일러의 확정 대입 분석이 layout과의 연결을
            // 추적하지 못해(CS0165) layout을 먼저 null로 초기화해둬야 한다.
            FormationLayout layout = null;
            var hasLayout = formationReader != null && formationReader.TryLoadCurrent(out layout);
            // 배치가 없을 때(hasLayout=false) 스폰 반지름/도주 이탈 거리를 계산할 기준 열 수 -
            // 아군이 없으면 어차피 즉시 패배하므로 정확한 값이 중요하지 않지만, 계산 자체는 항상
            // 유효한 columnCount를 필요로 한다. FormationLayout.DefaultColumnCount가 FormationGridView
            // 기본값과 공유하는 단일 출처다.
            var columnCount = hasLayout ? layout.ColumnCount : FormationLayout.DefaultColumnCount;
            var spawnCenter = FieldGeometry.ComputeSpawnPoint(spawnSelector.SelectSpawnPointIndex(), columnCount);
            var enemyMorale = new PartyMorale();
            var spawnRadius = FieldGeometry.ComputeSpawnRadius(columnCount);

            // 진영 공용 협력 객체를 필드로 채운다(설계 25번 §6.1) - 전투 중간에 Field 배치 타이머가
            // 완료돼 새 아군이 늦게 합류할 때(HandleFieldActivityCompleted) BuildSimulation() 스코프
            // 밖에서도 이번 전투와 같은 인스턴스를 재사용해야 하기 때문이다(안 그러면 새로 합류한
            // 유닛이 이미 싸우던 유닛과 다른 사기/방진 조율자를 갖게 된다 - BattleTestSimulationRule이
            // 같은 이유로 이미 필드 캐싱을 쓰는 것과 동일 패턴).
            midBattleFleeTravelDistance = FieldGeometry.ComputeFleeTravelDistance(columnCount);
            midBattleAllyMorale = new PartyMorale(); // 전투마다 새로 시작
            // tacticsReader가 없으면(인스톨러 미실행 등) 방향성 지시 없이 기존 동작으로 자연히
            // 폴백한다 - null을 넘기면 UnitTacticsBehaviors도 null이 되어 BattleCharacterUnit이
            // 적 유닛과 같은 경로(TickEngageWithoutTactics)를 탄다.
            midBattleTacticsProfileResolver = tacticsReader != null
                ? new UnitTacticsProfileResolver(tacticsReader, roleGroupMap)
                : null;
            midBattleStandardActivityRadius = FieldGeometry.ComputeStandardActivityRadius(columnCount);
            midBattleFieldRadius = FieldGeometry.ComputeFieldRadius(columnCount);
            // 사기 파동 조율자(Docs/설계/14번 §6) - fieldRadius가 있어야 소멸 조건을 계산할 수 있어
            // 그 직후 생성한다. PartyMorale과 마찬가지로 진영별 인스턴스, 전투마다 새로 시작.
            midBattleAllyWaveCoordinator = new MoraleWaveCoordinator(midBattleFieldRadius);
            var enemyWaveCoordinator = new MoraleWaveCoordinator(midBattleFieldRadius);
            // tacticsReader가 없으면 방향성 지시 자체가 비활성화되므로 파티 추적 설정을 읽을 수 없다 -
            // 이때는 어차피 Blocking 전열 후보가 하나도 없어(모든 RoleGroup이 null) 어떤 프리셋을
            // 넘기든 무해하다(기본값 OffensiveJudgment로 대체). BuildAllies보다 먼저 만들어야
            // BlockingPositioningStrategy(Docs/설계/12번 §12.12 7단계)에 주입할 수 있다.
            var partyPursuitPreset = tacticsReader?.GetPartySettings().Pursuit ?? "OffensiveJudgment";
            midBattleFrontlineCoordinator = new FrontlineFormationCoordinator(midBattleStandardActivityRadius, partyPursuitPreset);
            // 포위(Surround) 조율자(Docs/설계/12번 §13.3′) - frontlineCoordinator와 같은 이유로
            // BuildAllies보다 먼저 생성해야 SurroundPositioningStrategy에 주입할 수 있다. 군집화
            // 알고리즘 재사용을 위해 frontlineCoordinator 참조가 필요하다(§13.3′ "로직 공유").
            midBattleRangedSurroundCoordinator = new RangedSurroundCoordinator(midBattleStandardActivityRadius, midBattleFrontlineCoordinator);

            var allies = hasLayout ? BuildAllies(layout) : new List<IBattleCombatant>();
            var enemies = BuildEnemies(spawnCenter, enemyMorale, enemyWaveCoordinator, midBattleFleeTravelDistance);
            var protectedUnits = hasLayout ? BuildProtectedUnits(layout) : new List<IDamageable>();

            return new BattleSimulationLoop(allies, enemies, protectedUnits, midBattleFieldRadius, spawnRadius, midBattleFrontlineCoordinator, midBattleRangedSurroundCoordinator, midBattleAllyWaveCoordinator, enemyWaveCoordinator);
        }

        private List<IBattleCombatant> BuildAllies(FormationLayout layout)
        {
            var allies = new List<IBattleCombatant>();

            for (var slotIndex = 0; slotIndex < layout.SlotCount; slotIndex++)
            {
                var unit = BuildSingleAlly(layout, slotIndex);
                if (unit != null) allies.Add(unit);
            }
            return allies;
        }

        // 슬롯 하나를 BattleCharacterUnit 하나로 변환한다(설계 25번 §6.1) - BuildAllies()의 초기
        // 구성과 HandleFieldActivityCompleted()의 전투 중 늦은 합류가 이 메서드를 공유한다. 진영
        // 공용 협력 객체는 BuildSimulation()이 채워둔 midBattleXxx 필드를 그대로 참조한다.
        private BattleCharacterUnit BuildSingleAlly(FormationLayout layout, int slotIndex)
        {
            var unitId = layout.GetUnitId(slotIndex);
            if (unitId == null) return null;

            var rosterUnit = FindRosterUnit(unitId);
            if (rosterUnit == null || rosterUnit.Kind != FormationUnitKind.Character) return null;

            // 사망한 개체는 그 상행 동안 배치 불가라 정상 흐름에선 정비창 팔레트가 이미 막지만
            // (기획 13번 §6, 설계 16번), 방어적으로 한 번 더 확인한다.
            if (unitConditionRepository != null && unitConditionRepository.IsDead(unitId)) return null;

            // 직업 정보가 없는 Character(로스터 구현체가 IMercenaryUnit이 아닌 경우)는 예외적
            // 상황이라 Warrior를 기본값으로 둔다 - 정식 로스터 시스템이 생기면 모든 Character가
            // IMercenaryUnit을 구현하게 되어 이 분기 자체가 필요 없어질 것으로 예상된다.
            var mercenaryClass = rosterUnit is IMercenaryUnit mercenaryUnit ? mercenaryUnit.Class : "Warrior";

            var column = slotIndex % layout.ColumnCount;
            var row = slotIndex / layout.ColumnCount;
            var position = ComputeAllyPositionForSlot(layout, unitId, column, row);
            var stats = statProvider.GetStats(mercenaryClass);

            // 저장된 HP가 있으면(상행 중 이전 전투에서 입은 피해) 이번 전투의 시작 체력으로 쓴다 -
            // 만피(stats.MaxHp)는 직업 기준 고정값 그대로 두고 건드리지 않는다(설계 23번 §1/§2,
            // 기획 18번). 체력 게이지바가 항상 풀피로 보이던 문제(만피 자체가 저장값으로 재정의됨)의
            // 정정.
            float? startingHp = null;
            if (unitConditionRepository != null && unitConditionRepository.TryGetCurrentHp(unitId, out var savedHp))
            {
                startingHp = savedHp;
            }

            // 배치 슬롯 좌표(position)가 곧 방향성 지시의 HomePosition이다 - "정비창 슬롯 좌표"라는
            // 같은 개념을 두 번 계산하지 않는다.
            UnitTacticsBehaviors tacticsBehaviors = null;
            if (midBattleTacticsProfileResolver != null)
            {
                var profile = midBattleTacticsProfileResolver.Resolve(mercenaryClass, position);
                tacticsBehaviors = UnitTacticsBehaviorsFactory.Build(profile, midBattleStandardActivityRadius, midBattleFieldRadius, spatialQuery, midBattleFrontlineCoordinator, midBattleRangedSurroundCoordinator);
            }

            var characterUnit = new BattleCharacterUnit(position, isAlly: true, stats, damageFormula, midBattleAllyMorale, midBattleAllyWaveCoordinator, spatialQuery, midBattleFleeTravelDistance, tacticsBehaviors, startingHp: startingHp);
            allyUnitIds.Add((unitId, characterUnit));
            return characterUnit;
        }

        // 이동 중(설계 25번 §3.3) 인카운터로 중단된 유닛은 슬롯 좌표가 아니라 회피 경로
        // (PathSlotIndices) 위의 보간 위치에서 전투를 시작한다(기획 20번 §3.3, 설계 25번 §7). 출발/
        // 도착 두 점만 Lerp하면 경로가 장애물을 피해 굴곡질 때 중단 지점이 실제 경로에서 벗어나
        // 인접한 다른 유닛의 슬롯 위치와 겹쳐 소환되는 버그(실전 검증 2026-09-07)가 있었다 - 정비창
        // UI의 이동 아이콘과 같은 보간 수학(FormationPathInterpolation)을 전장 좌표계로 공유해
        // 해결한다. 진행 중인 이동이 없으면(가장 흔한 경우) 기존 슬롯 좌표를 그대로 쓴다.
        private Vector2 ComputeAllyPositionForSlot(FormationLayout layout, string unitId, int column, int row)
        {
            if (fieldActivityRepository != null && fieldActivityRepository.TryGetActivity(unitId, out var activity)
                && activity.Kind == FormationActivityKind.Moving && activity.PathSlotIndices.Count > 0)
            {
                var waypoints = new List<Vector2>(activity.PathSlotIndices.Count);
                foreach (var slotIndex in activity.PathSlotIndices)
                {
                    waypoints.Add(FieldPositionLayout.ComputeAllyPosition(slotIndex % layout.ColumnCount, slotIndex / layout.ColumnCount, layout.ColumnCount));
                }
                // 도착 고스트로 중도 수정된 이동은 부분 구간(연속 좌표)을 가질 수 있다(기획 21번,
                // 설계 26번 §2) - UI 쪽(FormationGridView.BuildWaypoints)과 동일하게 반영해야 인카운터
                // 중단 지점이 화면에 보이던 위치와 어긋나지 않는다. 대각선 구간(√2배, 설계 26번 §10)이
                // 섞일 수 있어 균등 보간이 아니라 구간별 실제 비용 배열로 보간한다.
                FormationPathInterpolation.ApplyPartialSegment(waypoints, activity.PartialSegmentIndex, activity.PartialSegmentWeight);
                var weights = FormationPathFinder.ComputeSegmentWeights(activity.PathSlotIndices, layout.ColumnCount, activity.PartialSegmentIndex, activity.PartialSegmentWeight);
                return FormationPathInterpolation.Evaluate(waypoints, activity.Progress01, weights);
            }

            return FieldPositionLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
        }

        private List<IBattleCombatant> BuildEnemies(Vector2 spawnCenter, PartyMorale enemyMorale, MoraleWaveCoordinator enemyWaveCoordinator, float fleeTravelDistance)
        {
            return enemyProvider.GetEncounterComposition()
                .Select(enemyStats => (IBattleCombatant)new BattleCharacterUnit(
                    spawnCenter, isAlly: false, enemyStats, damageFormula, enemyMorale, enemyWaveCoordinator, spatialQuery, fleeTravelDistance,
                    icon: BattlePlaceholderSprite.ForEnemyType(enemyStats.EnemyType)))
                .ToList();
        }

        private List<IDamageable> BuildProtectedUnits(FormationLayout layout)
        {
            var result = new List<IDamageable>();
            for (var slotIndex = 0; slotIndex < layout.SlotCount; slotIndex++)
            {
                var unitId = layout.GetUnitId(slotIndex);
                if (unitId == null) continue;

                var rosterUnit = FindRosterUnit(unitId);
                if (rosterUnit == null || rosterUnit.Kind == FormationUnitKind.Character) continue;

                var column = slotIndex % layout.ColumnCount;
                var row = slotIndex / layout.ColumnCount;
                var position = FieldPositionLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
                result.Add(new BattleProtectedUnit(position, ProtectedUnitTuning.MaxHp, rosterUnit.Icon));
            }
            return result;
        }

        // 슬롯의 unitId가 실제로 어떤 유닛(종류/직업)인지는 로스터를 순회해야 알 수 있다(FormationLayout
        // 자체엔 그 정보가 없다). 로스터가 커지면 Dictionary 캐싱으로 바꿀 대상.
        private IFormationUnit FindRosterUnit(string unitId)
        {
            if (rosterProvider == null) return null;

            foreach (var unit in rosterProvider.GetRoster())
            {
                if (unit.Id == unitId) return unit;
            }
            return null;
        }
    }
}
