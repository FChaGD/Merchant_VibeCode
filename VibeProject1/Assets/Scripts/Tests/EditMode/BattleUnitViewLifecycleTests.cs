using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 풀에서 재사용되는 전투 뷰의 바인딩 대칭(Docs/Refactor/2026-10-26_씬전용.md A-2, 적용 조건 ①) - 반환(Unbind) 뒤에는 이전 유닛 이벤트에
    /// 반응하지 않고, 사망 연출 뒤 다시 바인딩하면 색·크기가 새 유닛 기준으로 돌아와야 한다.
    /// </summary>
    public class BattleUnitViewLifecycleTests
    {
        private sealed class FakeUnit : IBattleCombatant
        {
            public FakeUnit(bool isAlly, float maxHp) { IsAlly = isAlly; MaxHp = maxHp; CurrentHp = maxHp; }
            public Vector2 Position => Vector2.zero;
            public bool IsAlive { get; set; } = true;
            public float Defense => 0f;
            public float MaxHp { get; }
            public float CurrentHp { get; set; }
            public float Attack => 0f;
            public float Range => 0f;
            public Sprite Icon => null;
            public bool IsAlly { get; }
            public bool IsFleeing => false;
            public Vector2 FleeVelocity => Vector2.zero;
            public IDamageable CurrentTarget => null;
            public string RoleGroup => null;
            public string Positioning => null;
            public IReadOnlyCollection<IDamageable> RecognizedEnemies => Array.Empty<IDamageable>();
            public Vector2? DebugMoveTarget => null;
            public event Action OnDied;
            public event Action<float> OnDamaged;
            public event Action OnFled;
            public event Action<IDamageable> OnAttacked { add { } remove { } }
            public void TakeDamage(float amount, IBattleCombatant attacker) { }
            public void ReceiveMoraleWave(float delta) { }
            public void NotifyKilledEnemy() { }
            public void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits) { }
            public void RaiseDied() { IsAlive = false; OnDied?.Invoke(); }
            public void RaiseDamaged() => OnDamaged?.Invoke(1f);
            public int Subscribers => (OnDied?.GetInvocationList().Length ?? 0) + (OnDamaged?.GetInvocationList().Length ?? 0) + (OnFled?.GetInvocationList().Length ?? 0);
        }

        private GameObject go;
        private BattleCharacterUnitView view;
        private SpriteRenderer body;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("View", typeof(SpriteRenderer));
            body = go.GetComponent<SpriteRenderer>();
            view = go.AddComponent<BattleCharacterUnitView>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(go);

        [Test]
        public void Unbind_StopsReactingToUnitEvents()
        {
            var unit = new FakeUnit(isAlly: true, maxHp: 100f);
            view.Bind(unit, null);
            Assert.AreEqual(3, unit.Subscribers, "피격·사망·도주를 구독한다");

            view.Unbind();
            Assert.AreEqual(0, unit.Subscribers);
            var color = body.color;
            var scale = go.transform.localScale;
            unit.RaiseDamaged();
            unit.RaiseDied();
            Assert.AreEqual(color, body.color);
            Assert.AreEqual(scale, go.transform.localScale);
        }

        [Test]
        public void CharacterView_Rebind_AfterDeath_RestoresVisuals()
        {
            var first = new FakeUnit(isAlly: false, maxHp: 100f);
            view.Bind(first, null);
            first.RaiseDied(); // 사망 틴트 + 1.1배
            view.Unbind();

            var second = new FakeUnit(isAlly: true, maxHp: 100f);
            view.Bind(second, null);
            Assert.AreEqual(new Color(0.3f, 0.5f, 1f, 1f), body.color, "아군 진영 색·알파 1로 돌아온다");
            Assert.AreEqual(Vector3.one * 0.6f, go.transform.localScale, "크기는 새 유닛 기준(MaxHp 100 → 0.6)");
        }
    }
}
