using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 방진선(저지형) 조율자 - 전투 1회당 1개, BattleSimulationLoop이 필드로 소유한다(PartyMorale과
    /// 같은 자리, Docs/설계/12번 §12.2). 유닛 개별이 아니라 allies/enemies/protectedUnits 전부에
    /// 접근 가능한 위치여야 해서 도입했다 - BattleCharacterUnit은 protectedUnits 목록에 접근할
    /// 방법이 없다는 구조적 제약 때문(§0에서 확인).
    ///
    /// 활성 라인 상태 보관, §12.7 클러스터링, §12.3~§12.5 기하 계산(anchorCenter/canonicalPoint 등),
    /// §12.9 교차 처리, §12.4 Update 전체 조립을 담당한다. 슬롯 배정/재배치(§12.6)는
    /// FrontlineFormationLine이 스스로 관리한다(Join/Leave/EvictOutOfBounds).
    /// </summary>
    public class FrontlineFormationCoordinator
    {
        // 전투 내내 고정 - standardActivityRadius(대형 크기 기반)와 파티 추적 설정 둘 다 전투 시작
        // 시점에 확정되는 값이라(Docs/설계/12번 §2, §12.3-1), 매 틱 인자로 받을 이유가 없다.
        private readonly StandardActivityRadiusZone standardRadiusZone;
        private readonly PursuitPreset partyPursuitPreset;

        private readonly List<FrontlineFormationLine> activeLines = new();
        public IReadOnlyList<FrontlineFormationLine> ActiveLines => activeLines;

        public FrontlineFormationCoordinator(float standardActivityRadius, PursuitPreset partyPursuitPreset)
        {
            standardRadiusZone = new StandardActivityRadiusZone(standardActivityRadius);
            this.partyPursuitPreset = partyPursuitPreset;
        }

        // §12.7 클러스터링 재사용 버퍼 - EnemyRecognitionTrackerBase.recognizedBuffer와 같은 이유로
        // 매 틱 새 List/Dictionary를 할당하지 않는다(§12.8). unionFindParent의 인덱스는 매 호출
        // 인자로 받은 enemies 리스트의 인덱스와 그대로 대응한다.
        private readonly List<int> unionFindParent = new();
        private readonly Dictionary<int, int> clusterIndexByRoot = new();
        private readonly List<List<IDamageable>> clusterPool = new();
        private readonly List<IReadOnlyList<IDamageable>> clusterResultBuffer = new();

        // §12.3 보호대상 후보군 재사용 버퍼 - Update 1회(틱)당 한 번만 채우고 그 틱의 모든 라인
        // 계산이 공유해서 쓴다(§12.8과 같은 이유로 매 라인마다 다시 스캔하지 않음).
        private readonly List<IDamageable> protectionCandidateBuffer = new();
        // ComputeAnchorCenter 내부에서만 쓰는 중복 제거용 재사용 버퍼.
        private readonly List<IDamageable> anchorCandidateBuffer = new();
        // §12.9 교차 처리 - EvictOutOfBounds가 채우는 재사용 버퍼(라인당 재사용, 다음 라인 처리 전
        // 그대로 소비되므로 라인 간 공유해도 안전하다).
        private readonly List<IBattleCombatant> relocationBuffer = new();

        // §12.4 Update 조립용 재사용 버퍼.
        private readonly List<IDamageable> recognizedUnionBuffer = new();
        private readonly List<IBattleCombatant> deadOrFleeingBuffer = new();
        private readonly List<IBattleCombatant> unassignedPoolBuffer = new();
        private readonly List<IBattleCombatant> recognizingSubsetBuffer = new();
        private readonly List<IDamageable> poolRecognizedEnemiesBuffer = new();
        private readonly List<IBattleCombatant> joinedBuffer = new();

        /// <summary>
        /// 적들을 거리 임계값(TacticsTuning.ClusterMergeDistanceMeters) 기준으로 군집화한다 - A-B가
        /// 가깝고 B-C가 가까우면 A/B/C가 전이적으로 한 군집이 된다(Union-Find, Docs/설계/12번 §12.7).
        /// 적 수가 적어(현재 규모 최대 5) O(n^2) 쌍 비교로 충분해 별도 공간 분할 자료구조는 쓰지 않는다.
        /// 반환값은 재사용 버퍼라 다음 호출 전까지만 유효하다.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<IDamageable>> ClusterEnemies(IReadOnlyList<IDamageable> enemies)
        {
            unionFindParent.Clear();
            for (var i = 0; i < enemies.Count; i++) unionFindParent.Add(i);

            for (var i = 0; i < enemies.Count; i++)
            {
                for (var j = i + 1; j < enemies.Count; j++)
                {
                    var distance = (enemies[i].Position - enemies[j].Position).magnitude;
                    if (distance <= TacticsTuning.ClusterMergeDistanceMeters)
                    {
                        Union(i, j);
                    }
                }
            }

            clusterIndexByRoot.Clear();
            var clusterCount = 0;
            for (var i = 0; i < enemies.Count; i++)
            {
                var root = Find(i);
                if (!clusterIndexByRoot.TryGetValue(root, out var clusterIndex))
                {
                    clusterIndex = clusterCount++;
                    clusterIndexByRoot[root] = clusterIndex;
                    if (clusterIndex == clusterPool.Count) clusterPool.Add(new List<IDamageable>());
                    clusterPool[clusterIndex].Clear();
                }
                clusterPool[clusterIndex].Add(enemies[i]);
            }

            clusterResultBuffer.Clear();
            for (var i = 0; i < clusterCount; i++) clusterResultBuffer.Add(clusterPool[i]);
            return clusterResultBuffer;
        }

        /// <summary>
        /// 보호대상 후보군을 모은다(Docs/설계/12번 §12.3) - 마차/시설(protectedUnits) 전부 + 아군 중
        /// RangedDealer/Supporter 역할군. 죽은 대상은 더 이상 보호할 이유가 없어 제외한다. 틱당 한 번만
        /// 호출해 그 틱의 모든 라인 계산이 결과를 공유하는 용도(재사용 버퍼, 다음 호출 전까지만 유효).
        /// </summary>
        public IReadOnlyList<IDamageable> ComputeProtectionCandidates(IReadOnlyList<IDamageable> protectedUnits, IReadOnlyList<IBattleCombatant> allies)
        {
            protectionCandidateBuffer.Clear();

            foreach (var unit in protectedUnits)
            {
                if (unit.IsAlive) protectionCandidateBuffer.Add(unit);
            }
            foreach (var ally in allies)
            {
                if (ally.IsAlive && ally.RoleGroup is RoleGroup.RangedDealer or RoleGroup.Supporter)
                {
                    protectionCandidateBuffer.Add(ally);
                }
            }

            return protectionCandidateBuffer;
        }

        /// <summary>
        /// 한 군집(라인이 상대하는 적들)의 라인 위치·방향을 계산한다(Docs/설계/12번 §12.5).
        /// </summary>
        public FrontlineLineGeometry ComputeLineGeometry(IReadOnlyList<IDamageable> clusterEnemies, IReadOnlyList<IDamageable> protectionCandidates)
        {
            var anchorCenter = ComputeAnchorCenter(clusterEnemies, protectionCandidates);
            var enemyCenter = ComputeAveragePosition(clusterEnemies);

            var toEnemy = enemyCenter - anchorCenter;
            // 0벡터 가드(§12.5) - 적이 anchorCenter와 정확히 겹치면 axisDir=zero, canonicalPoint=anchorCenter.
            var axisDir = toEnemy.sqrMagnitude > 0.0001f ? toEnemy.normalized : Vector2.zero;
            var range = ComputeMaxRange(clusterEnemies);

            var canonicalPoint = standardRadiusZone.ClampToZone(anchorCenter + axisDir * range);
            var lineDir = new Vector2(-axisDir.y, axisDir.x); // axisDir을 90도 회전.

            return new FrontlineLineGeometry(anchorCenter, enemyCenter, axisDir, range, canonicalPoint, lineDir);
        }

        /// <summary>
        /// 전투 시뮬레이션 1틱 - 각 유닛 Tick 이전에 실행된다(Docs/설계/12번 §12.4). 원래 설계
        /// pseudocode는 enemies도 인자로 받았으나, recognizedUnion을 매 유닛의 RecognizedEnemies
        /// 스냅샷에서 읽는 이 구현에서는 전역 적 목록을 다시 훑을 일이 없어 필요하지 않았다.
        /// §12.4의 "4. 슬롯 배정"은 별도 단계가 아니다 - Join/Leave/EvictOutOfBounds가 이미
        /// 소속 변경 시점에 이벤트성으로 슬롯을 배정하므로(§12.6) 여기서 다시 할 일이 없다.
        /// </summary>
        public void Update(float deltaTime, IReadOnlyList<IBattleCombatant> allies, IReadOnlyList<IDamageable> protectedUnits)
        {
            var protectionCandidates = ComputeProtectionCandidates(protectedUnits, allies);

            ReconcileExistingLines(deltaTime, protectionCandidates);
            AssignUnassignedPool(allies, protectionCandidates);
            ResolveIntersections();
        }

        // §12.4 1단계 - 기존 라인 재조정(사망/도주 제거 → recognizedUnion 재계산 → 해체 또는
        // 정지/전진/당겨오기 판단). 뒤에서부터 순회해 해체된 라인을 그 자리에서 제거해도 안전하다.
        private void ReconcileExistingLines(float deltaTime, IReadOnlyList<IDamageable> protectionCandidates)
        {
            for (var i = activeLines.Count - 1; i >= 0; i--)
            {
                var line = activeLines[i];
                RemoveDeadOrFleeingMembers(line);

                var recognizedUnion = ComputeRecognizedUnion(line);
                if (recognizedUnion.Count == 0)
                {
                    // 해체 - 소속 유닛의 개별 상태는 여기서 건드리지 않는다(§0 - Returning 전환은
                    // 7단계에서 BlockingPositioningStrategy가 코디네이터 슬롯 유무를 참조하게 되면
                    // 자연히 연결된다. 이 유닛들은 다음 틱 "미배정 풀"에서 다시 평가된다).
                    activeLines.RemoveAt(i);
                    continue;
                }

                var geometry = ComputeLineGeometry(recognizedUnion, protectionCandidates);
                line.EnemyCount = recognizedUnion.Count;

                var isOutsideStandardRadius = !standardRadiusZone.Contains(line.LinePoint);
                var hasEngagedMember = HasEngagedMember(line);
                // justLandedHit 대체 - "이번 틱 실제로 명중했는지"는 라인 단위로 노출돼 있지 않아,
                // "교전 가능 상태(사거리 안 타겟 보유)"로 근사한다(OffensiveJudgment 프리셋에만 영향).
                if (line.PursuitPolicy.ShouldDisengage(deltaTime, isOutsideStandardRadius, hasEngagedMember))
                {
                    line.LinePoint = geometry.CanonicalPoint;
                    line.LineDir = geometry.LineDir;
                }
                else if (!hasEngagedMember && line.Members.Count > recognizedUnion.Count)
                {
                    var step = TacticsTuning.LineAdvanceSpeedMetersPerSecond * deltaTime;
                    line.LinePoint = Vector2.MoveTowards(line.LinePoint, geometry.EnemyCenter, step);
                    line.LineDir = geometry.LineDir;
                }
                // 그 외(정지) - 아무 것도 갱신하지 않는다(§12.5 - "정지 상태에서는 라인이 완전히 고정된다").
            }
        }

        // §12.4 2단계 - 아직 라인에 없는 Blocking 전열을 모아, 인식한 적이 있는 유닛부터 군집 단위로
        // 새 라인을 그린다. 한 군집으로 슬롯 배정(상행 전체 반경 안)에 성공하는 유닛이 하나도 없으면
        // 그 자리에서 멈춘다(pseudocode 그대로 - 남은 pool은 이번 틱엔 전부 "대기").
        private void AssignUnassignedPool(IReadOnlyList<IBattleCombatant> allies, IReadOnlyList<IDamageable> protectionCandidates)
        {
            unassignedPoolBuffer.Clear();
            foreach (var ally in allies)
            {
                if (ally.IsAlive && !ally.IsFleeing && ally.RoleGroup == RoleGroup.Frontline
                    && ally.Positioning == LocalPositioning.Blocking && !IsInAnyActiveLine(ally))
                {
                    unassignedPoolBuffer.Add(ally);
                }
            }

            while (true)
            {
                recognizingSubsetBuffer.Clear();
                foreach (var candidate in unassignedPoolBuffer)
                {
                    if (candidate.RecognizedEnemies.Count > 0) recognizingSubsetBuffer.Add(candidate);
                }
                if (recognizingSubsetBuffer.Count == 0) break;

                poolRecognizedEnemiesBuffer.Clear();
                foreach (var candidate in recognizingSubsetBuffer)
                {
                    foreach (var enemy in candidate.RecognizedEnemies)
                    {
                        if (enemy.IsAlive) AddDistinct(poolRecognizedEnemiesBuffer, enemy);
                    }
                }

                var clusters = ClusterEnemies(poolRecognizedEnemiesBuffer);
                if (clusters.Count == 0) break;

                var geometry = ComputeLineGeometry(clusters[0], protectionCandidates);
                var newLine = new FrontlineFormationLine { LinePoint = geometry.CanonicalPoint, LineDir = geometry.LineDir };

                joinedBuffer.Clear();
                foreach (var candidate in unassignedPoolBuffer)
                {
                    if (newLine.Join(candidate) && newLine.TryGetSlotPosition(candidate, out var slotPosition)
                        && standardRadiusZone.Contains(slotPosition))
                    {
                        joinedBuffer.Add(candidate);
                    }
                    else
                    {
                        newLine.Leave(candidate); // 슬롯이 반경 밖이거나 배정 실패 - 이 라인엔 합류시키지 않는다.
                    }
                }
                if (joinedBuffer.Count == 0) break;

                newLine.EnemyCount = clusters[0].Count;
                newLine.PursuitPolicy = PursuitPolicyFactory.Create(partyPursuitPreset);
                activeLines.Add(newLine);

                foreach (var candidate in joinedBuffer) unassignedPoolBuffer.Remove(candidate);
            }
        }

        private void RemoveDeadOrFleeingMembers(FrontlineFormationLine line)
        {
            deadOrFleeingBuffer.Clear();
            foreach (var member in line.Members)
            {
                if (!member.IsAlive || member.IsFleeing) deadOrFleeingBuffer.Add(member);
            }
            foreach (var member in deadOrFleeingBuffer) line.Leave(member);
        }

        private IReadOnlyList<IDamageable> ComputeRecognizedUnion(FrontlineFormationLine line)
        {
            recognizedUnionBuffer.Clear();
            foreach (var member in line.Members)
            {
                foreach (var enemy in member.RecognizedEnemies)
                {
                    if (enemy.IsAlive) AddDistinct(recognizedUnionBuffer, enemy);
                }
            }
            return recognizedUnionBuffer;
        }

        // "교전 가능" = 살아있는 타겟이 있고 사거리 안(§12.4) - 정지 판단과 이탈 판정(justLandedHit
        // 근사) 둘 다 이 값을 쓴다.
        private static bool HasEngagedMember(FrontlineFormationLine line)
        {
            foreach (var member in line.Members)
            {
                if (member.CurrentTarget is { IsAlive: true }
                    && (member.CurrentTarget.Position - member.Position).sqrMagnitude <= member.Range * member.Range)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsInAnyActiveLine(IBattleCombatant ally)
        {
            foreach (var line in activeLines)
            {
                if (line.Members.Contains(ally)) return true;
            }
            return false;
        }

        /// <summary>
        /// 활성 라인 전부에 대해 교차를 처리한다(Docs/설계/12번 §12.9) - 필렛(곡선 병합) 안은 방향
        /// 결함이 발견돼 폐기됐고, 대신 "선 긋기 중단"으로 확정됐다. 매 틱 제한을 처음부터 다시
        /// 계산한다 - 라인이 움직여 교차가 풀리면 제한도 함께 풀려야 하기 때문이다. 손실분 보충
        /// (규칙3)은 별도 로직 없이 EvictOutOfBounds 이후 Join의 정상 빈 슬롯 탐색이 자동으로
        /// 반대편에 새 슬롯을 만들어 처리한다.
        /// </summary>
        public void ResolveIntersections()
        {
            foreach (var line in activeLines)
            {
                line.MinAllowedOffset = null;
                line.MaxAllowedOffset = null;
            }

            for (var i = 0; i < activeLines.Count; i++)
            {
                for (var j = i + 1; j < activeLines.Count; j++)
                {
                    ResolveIntersection(activeLines[i], activeLines[j]);
                }
            }

            foreach (var line in activeLines)
            {
                line.EvictOutOfBounds(relocationBuffer);
                foreach (var member in relocationBuffer)
                {
                    line.Join(member);
                }
            }
        }

        // 규칙1(선 긋기 중단)+규칙2(교차점 슬롯 소유권) - 평행(방향 없음 포함)이면 교차 없음.
        private void ResolveIntersection(FrontlineFormationLine lineA, FrontlineFormationLine lineB)
        {
            if (!TryComputeIntersectionOffsets(lineA, lineB, out var tA, out var tB)) return;

            var boundaryA = Mathf.RoundToInt(tA);
            var boundaryB = Mathf.RoundToInt(tB);
            var aWins = DecideIntersectionWinner(lineA, lineB);

            ApplyBoundary(lineA, tA, boundaryA, keepBoundaryOffset: aWins);
            ApplyBoundary(lineB, tB, boundaryB, keepBoundaryOffset: !aWins);
        }

        // 규칙2 - 소속 인원이 많은 라인 승리 → 동률이면 상대하는 적(recognizedUnion) 수가 적은
        // 라인 승리 → 그것도 같으면 랜덤.
        private static bool DecideIntersectionWinner(FrontlineFormationLine lineA, FrontlineFormationLine lineB)
        {
            if (lineA.Members.Count != lineB.Members.Count) return lineA.Members.Count > lineB.Members.Count;
            if (lineA.EnemyCount != lineB.EnemyCount) return lineA.EnemyCount < lineB.EnemyCount;
            return Random.value < 0.5f;
        }

        // 교차점을 넘어서는 방향으로는 더 이상 슬롯을 두지 않는다(규칙1). 경계 슬롯 자체는 승자만
        // 유지하고(keepBoundaryOffset), 패자는 그 슬롯도 내준다(경계보다 한 칸 안쪽까지만 허용).
        // 이미 걸린 제한보다 느슨하면 무시한다 - 여러 교차가 겹칠 때 가장 좁은 제한이 이겨야 한다.
        private static void ApplyBoundary(FrontlineFormationLine line, float t, int boundaryOffset, bool keepBoundaryOffset)
        {
            var directionSign = t >= 0f ? 1 : -1;
            var limitOffset = keepBoundaryOffset ? boundaryOffset : boundaryOffset - directionSign;

            if (directionSign > 0)
            {
                if (!line.MaxAllowedOffset.HasValue || limitOffset < line.MaxAllowedOffset.Value)
                {
                    line.MaxAllowedOffset = limitOffset;
                }
            }
            else
            {
                if (!line.MinAllowedOffset.HasValue || limitOffset > line.MinAllowedOffset.Value)
                {
                    line.MinAllowedOffset = limitOffset;
                }
            }
        }

        // 두 무한 직선(linePoint + t*lineDir)의 교차 파라미터를 각자 기준으로 구한다(표준 벡터 교차
        // 공식). denom이 0에 가까우면 평행(방향이 0인 퇴화 라인 포함) - 교차 없음.
        private static bool TryComputeIntersectionOffsets(FrontlineFormationLine lineA, FrontlineFormationLine lineB, out float tA, out float tB)
        {
            var dA = lineA.LineDir;
            var dB = lineB.LineDir;
            var denom = dA.x * dB.y - dA.y * dB.x;
            if (Mathf.Abs(denom) < 0.0001f)
            {
                tA = 0f;
                tB = 0f;
                return false;
            }

            var diff = lineB.LinePoint - lineA.LinePoint;
            tA = (diff.x * dB.y - diff.y * dB.x) / denom;
            tB = (diff.x * dA.y - diff.y * dA.x) / denom;
            return true;
        }

        // 군집에 속한 적들이 (a) CurrentTarget이 후보군의 원소이거나, (b) 타겟팅 여부와 무관하게
        // 후보군 중 가장 가까운 것 - 이 조건을 만족하는 후보들의 위치 평균(§12.3). 후보군 자체가
        // 비어있으면(극단적 경우) 상행 대열 중심(원점)으로 대체한다.
        private Vector2 ComputeAnchorCenter(IReadOnlyList<IDamageable> clusterEnemies, IReadOnlyList<IDamageable> protectionCandidates)
        {
            if (protectionCandidates.Count == 0) return Vector2.zero;

            anchorCandidateBuffer.Clear();
            foreach (var enemy in clusterEnemies)
            {
                AddDistinct(anchorCandidateBuffer, FindNearest(enemy.Position, protectionCandidates));

                if (enemy is IBattleCombatant combatant && combatant.CurrentTarget != null
                    && ContainsReference(protectionCandidates, combatant.CurrentTarget))
                {
                    AddDistinct(anchorCandidateBuffer, combatant.CurrentTarget);
                }
            }

            return anchorCandidateBuffer.Count > 0 ? ComputeAveragePosition(anchorCandidateBuffer) : Vector2.zero;
        }

        private static void AddDistinct(List<IDamageable> list, IDamageable candidate)
        {
            if (candidate != null && !ContainsReference(list, candidate)) list.Add(candidate);
        }

        private static bool ContainsReference(IReadOnlyList<IDamageable> list, IDamageable value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value)) return true;
            }
            return false;
        }

        private static IDamageable FindNearest(Vector2 position, IReadOnlyList<IDamageable> candidates)
        {
            IDamageable nearest = null;
            var nearestSqrDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                var sqrDistance = (candidate.Position - position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private static Vector2 ComputeAveragePosition(IReadOnlyList<IDamageable> units)
        {
            if (units.Count == 0) return Vector2.zero;

            var sum = Vector2.zero;
            foreach (var unit in units) sum += unit.Position;
            return sum / units.Count;
        }

        private static float ComputeMaxRange(IReadOnlyList<IDamageable> units)
        {
            var maxRange = 0f;
            foreach (var unit in units)
            {
                if (unit.Range > maxRange) maxRange = unit.Range;
            }
            return maxRange;
        }

        // 경로 압축(path halving)만 적용 - 규모가 작아(§12.8) 랭크 기반 합병까지는 과설계.
        private int Find(int index)
        {
            while (unionFindParent[index] != index)
            {
                unionFindParent[index] = unionFindParent[unionFindParent[index]];
                index = unionFindParent[index];
            }
            return index;
        }

        private void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB) unionFindParent[rootA] = rootB;
        }
    }
}
