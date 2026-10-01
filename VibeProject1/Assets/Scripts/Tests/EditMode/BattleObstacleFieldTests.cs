using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class BattleObstacleFieldTests
    {
        private const float Tolerance = 0.01f;
        private const float StandardHalfSize = ProtectedUnitTuning.BodySize * 0.5f;
        private const float Speed = 1f;
        private const float DeltaTime = 0.02f;

        // 전투 좌표는 x = 행, y = 열(BattleFieldLayout, 간격 1) - 칸과 위치를 같은 규칙으로 맞춘다.
        private static ProtectedPlacement At(int column, int row, float halfSize = StandardHalfSize) =>
            new(new BattleProtectedUnit(new Vector2(row, column), ProtectedUnitTuning.MaxHp, null, halfSize, ProtectedUnitKind.Wagon), column, row);

        private static BattleObstacleField Field(params ProtectedPlacement[] placements) => new(placements);

        // 실제 캐릭터와 같은 순서(보정 → 조향 → 감속 반영 이동 → 빼내기)로 걸어 본다. 매 틱 밀림(push, 캐릭터끼리 밀어내기 흉내)을
        // 더할 수 있다. 빼내기 뒤 장애물 반경 안에 남은 적이 있는지, 감속된 틱 수, 마지막 위치를 돌려준다.
        private static (Vector2 final, bool penetrated, int slowTicks) Walk(
            BattleObstacleField field, Vector2 start, Vector2 destination, IDamageable ignored = null, Vector2 push = default, int steps = 4000)
        {
            var position = start;
            var state = default(ObstacleAvoidanceState);
            var penetrated = false;
            var slowTicks = 0;
            for (var i = 0; i < steps; i++)
            {
                var corrected = field.CorrectDestination(position, destination);
                if ((corrected - position).sqrMagnitude <= 0.0004f) break;

                var multiplier = field.GetSpeedMultiplier(position);
                if (multiplier < 1f) slowTicks++;
                position += (field.Steer(position, corrected, ignored, ref state) * Speed * multiplier + push) * DeltaTime;
                position = field.ResolvePenetration(position);
                foreach (var shape in field.Obstacles)
                {
                    if (shape.IsWithin(position, shape.Radius - 0.001f)) penetrated = true;
                }
            }
            return (position, penetrated, slowTicks);
        }

        [Test]
        public void Create_NoPlacements_ReturnsNullNavigator()
        {
            Assert.AreSame(NullObstacleNavigator.Instance, BattleObstacleField.Create(new List<ProtectedPlacement>()));
        }

        [Test]
        public void Size_FixedUpToReferenceSize_ProportionalAbove()
        {
            var standard = Field(At(0, 0)).Obstacles[0];
            Assert.AreEqual(0.5f, standard.Radius, Tolerance);
            Assert.AreEqual(0.25f, standard.Clearance, Tolerance);

            var small = Field(At(0, 0, 0.2f)).Obstacles[0];
            Assert.AreEqual(standard.Radius, small.Radius, Tolerance);
            Assert.AreEqual(standard.Clearance, small.Clearance, Tolerance);

            var large = Field(At(0, 0, 0.7f)).Obstacles[0];
            Assert.AreEqual(1.0f, large.Radius, Tolerance);
            Assert.AreEqual(0.5f, large.Clearance, Tolerance);
        }

        [Test]
        public void Passages_OnlyBetweenEightNeighbors()
        {
            Assert.AreEqual(1, Field(At(0, 0), At(1, 0)).Passages.Count, "가로·세로 이웃");
            Assert.AreEqual(1, Field(At(0, 0), At(1, 1)).Passages.Count, "대각선 이웃");
            Assert.AreEqual(0, Field(At(0, 0), At(2, 0)).Passages.Count, "한 칸 간격은 통로 아님");
            Assert.AreEqual(4, Field(At(0, 0), At(1, 0), At(2, 0), At(3, 0), At(4, 0)).Passages.Count, "일렬 5개 = 4통로");
        }

        [Test]
        public void SpeedMultiplier_HalfBetweenAdjacent_FullElsewhere()
        {
            var field = Field(At(0, 0), At(1, 0)); // 위치 (0,0)·(0,1)
            Assert.AreEqual(ObstacleAvoidanceTuning.PassageSpeedMultiplier, field.GetSpeedMultiplier(new Vector2(0f, 0.5f)), Tolerance);
            Assert.AreEqual(1f, field.GetSpeedMultiplier(new Vector2(1f, 0.5f)), Tolerance);
            // 대각선으로 붙은 두 마차 사이 빈 칸에 서 있으면 느려지지 않는다(통로 중심선에서 약 0.71).
            Assert.AreEqual(1f, Field(At(0, 0), At(1, 1)).GetSpeedMultiplier(new Vector2(1f, 0f)), Tolerance);
        }

        [Test]
        public void CorrectDestination_InsideAvoidRadius_MovesToBoundary()
        {
            var field = Field(At(0, 0));
            var corrected = field.CorrectDestination(new Vector2(-3f, 0f), new Vector2(0.3f, 0f));
            Assert.AreEqual(field.Obstacles[0].AvoidRadius, corrected.magnitude, Tolerance);
        }

        [Test]
        public void ResolvePenetration_InsideRadius_PushesToRadius()
        {
            var field = Field(At(0, 0));
            Assert.AreEqual(field.Obstacles[0].Radius, field.ResolvePenetration(new Vector2(0.1f, 0f)).magnitude, Tolerance);
        }

        [Test]
        public void Steer_HeadOn_PassesAroundSingleObstacleAndArrives()
        {
            var (final, penetrated, slowTicks) = Walk(Field(At(0, 0)), new Vector2(-3f, 0f), new Vector2(3f, 0f));
            Assert.IsFalse(penetrated);
            Assert.AreEqual(0, slowTicks);
            Assert.Less((final - new Vector2(3f, 0f)).magnitude, 0.1f);
        }

        [Test]
        public void Steer_HeadOn_KeepsChosenSideWhilePassing()
        {
            var field = Field(At(0, 0));
            var position = new Vector2(-2f, 0f);
            var state = default(ObstacleAvoidanceState);
            field.Steer(position, new Vector2(3f, 0f), null, ref state);
            Assert.IsTrue(state.HasValue);
            var side = state.Side;
            Assert.AreEqual(-1, side, "정면은 오른쪽");

            for (var i = 0; i < 100; i++)
            {
                position += field.Steer(position, new Vector2(3f, 0f), null, ref state) * Speed * DeltaTime;
                if (state.HasValue) Assert.AreEqual(side, state.Side);
            }
        }

        [Test]
        public void Steer_AttackTarget_IsNotAvoided()
        {
            var placement = At(0, 0);
            var field = Field(placement);
            var state = default(ObstacleAvoidanceState);
            var direction = field.Steer(new Vector2(-2f, 0f), Vector2.zero, placement.Unit, ref state);
            Assert.AreEqual(1f, direction.x, Tolerance);
            Assert.IsFalse(state.HasValue);
        }

        [Test]
        public void AdjacentWagons_PassBetweenAtReducedSpeed()
        {
            // 붙은 두 마차 사이(y 0.5)를 가로지르는 경로 - 통과하되 감속 구역을 지난다(설계 72번 §12).
            var field = Field(At(0, 0), At(1, 0));
            var (final, penetrated, slowTicks) = Walk(field, new Vector2(-3f, 0.5f), new Vector2(3f, 0.5f));
            Assert.IsFalse(penetrated);
            Assert.Greater(slowTicks, 0);
            Assert.Less((final - new Vector2(3f, 0.5f)).magnitude, 0.1f);
        }

        [Test]
        public void AdjacentWagons_PushedTowardGap_StillArrives()
        {
            var field = Field(At(0, 0), At(1, 0));
            var (final, penetrated, _) = Walk(field, new Vector2(-3f, 0.5f), new Vector2(3f, 0.5f), push: new Vector2(0.3f, 0f));
            Assert.IsFalse(penetrated);
            Assert.Less((final - new Vector2(3f, 0.5f)).magnitude, 0.1f);
        }

        [Test]
        public void DeepPocket_EscapesTowardClosedSide()
        {
            // 2026-10-01 실전 버그 - 깊은 ㄷ자(입구 +x, 안쪽 2칸) 안에서 막힌 뒤쪽으로 가려던 캐릭터가 갇혔다.
            var field = Field(At(0, 0), At(1, 0), At(2, 0), At(0, 1), At(2, 1), At(0, 2), At(2, 2));
            var (final, penetrated, _) = Walk(field, new Vector2(1f, 1f), new Vector2(-3f, 1f));
            Assert.IsFalse(penetrated);
            Assert.Less((final - new Vector2(-3f, 1f)).magnitude, 0.1f);
        }

        [Test]
        public void DestroyedObstacle_IsIgnored()
        {
            // 파괴돼도 전투가 계속되므로(기획 73번) 부서진 마차는 피하지도, 빼내지도 않는다(설계 74번 §2.6).
            var placement = At(0, 0);
            var field = Field(placement);
            placement.Unit.TakeDamage(ProtectedUnitTuning.MaxHp, null);

            var state = default(ObstacleAvoidanceState);
            Assert.AreEqual(1f, field.Steer(new Vector2(-2f, 0f), new Vector2(3f, 0f), null, ref state).x, Tolerance);
            Assert.IsFalse(state.HasValue);
            Assert.AreEqual(new Vector2(0.1f, 0f), field.ResolvePenetration(new Vector2(0.1f, 0f)));
            Assert.AreEqual(new Vector2(0.3f, 0f), field.CorrectDestination(new Vector2(-3f, 0f), new Vector2(0.3f, 0f)));
            Assert.IsFalse(field.IsObstacleActive(0));
        }

        [Test]
        public void Passage_WithDestroyedEnd_DoesNotSlow()
        {
            var a = At(0, 0);
            var field = Field(a, At(1, 0));
            a.Unit.TakeDamage(ProtectedUnitTuning.MaxHp, null);
            Assert.AreEqual(1f, field.GetSpeedMultiplier(new Vector2(0f, 0.5f)), Tolerance);
            Assert.IsFalse(field.IsPassageActive(0));
        }

        [Test]
        public void DiagonalPocketCell_IsReachable()
        {
            var field = Field(At(0, 0), At(1, 1));
            var home = new Vector2(1f, 0f);
            var (final, penetrated, _) = Walk(field, new Vector2(1f, -3f), home);
            Assert.IsFalse(penetrated);
            Assert.Less((final - home).magnitude, 0.1f);
        }
    }
}
