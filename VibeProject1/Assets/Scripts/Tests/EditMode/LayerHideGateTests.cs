using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 레이어 숨김 규칙 게이트(Docs/Refactor/2026-10-26_공통.md B-4). Hub 팝업 축(모달·시설 화면)과 Field 이벤트 가림이 같은 장치를 쓴다.
    /// 숨김은 CanvasGroup(alpha·raycast·interactable)로만 한다 - 오브젝트를 끄면 게이지·알림의 구독·코루틴이 끊긴다.
    /// </summary>
    public class LayerHideGateTests
    {
        private sealed class Condition : IUIHideCondition
        {
            public bool IsActive { get; private set; }
            public event Action Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public void Set(bool active) { IsActive = active; Changed?.Invoke(); }
        }

        private GameObject gateObject;
        private LayerHideGate gate;
        private GameObject layerObject;
        private CanvasGroup layer;

        [SetUp]
        public void SetUp()
        {
            gateObject = new GameObject("Gate");
            gate = gateObject.AddComponent<LayerHideGate>();
            layerObject = new GameObject("Layer", typeof(RectTransform), typeof(CanvasGroup));
            layer = layerObject.GetComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gateObject);
            if (layerObject != null) UnityEngine.Object.DestroyImmediate(layerObject);
        }

        [Test]
        public void HidesAndRestores()
        {
            var condition = new Condition();
            gate.Register(new[] { new LayerHideRule(condition, new[] { layer }) });
            Assert.AreEqual(1f, layer.alpha);

            condition.Set(true);
            Assert.AreEqual(0f, layer.alpha);
            Assert.IsFalse(layer.interactable);
            Assert.IsFalse(layer.blocksRaycasts);
            Assert.IsTrue(layerObject.activeSelf, "오브젝트는 끄지 않는다");

            condition.Set(false);
            Assert.AreEqual(1f, layer.alpha);
            Assert.IsTrue(layer.interactable);
            Assert.IsTrue(layer.blocksRaycasts);
        }

        [Test]
        public void Register_AppliesCurrentState()
        {
            var condition = new Condition();
            condition.Set(true);
            gate.Register(new[] { new LayerHideRule(condition, new[] { layer }) });
            Assert.AreEqual(0f, layer.alpha, "Field 재로드 때 이미 가림 상태면 즉시 반영");
        }

        [Test]
        public void TwoRulesSameLayer_HiddenUntilBothInactive()
        {
            var modal = new Condition();
            var screen = new Condition();
            gate.Register(new[] { new LayerHideRule(modal, new[] { layer }), new LayerHideRule(screen, new[] { layer }) });

            screen.Set(true);
            modal.Set(true);
            modal.Set(false);
            Assert.AreEqual(0f, layer.alpha, "시설 화면이 열려 있으면 모달을 닫아도 숨김 유지");

            screen.Set(false);
            Assert.AreEqual(1f, layer.alpha);
        }

        [Test]
        public void Register_ReplacesPreviousRules_Unsubscribes()
        {
            var first = new Condition();
            gate.Register(new[] { new LayerHideRule(first, new[] { layer }) });
            Assert.AreEqual(1, first.Subscribers);

            var second = new Condition();
            gate.Register(new[] { new LayerHideRule(second, new[] { layer }) });
            Assert.AreEqual(0, first.Subscribers);

            first.Set(true);
            Assert.AreEqual(1f, layer.alpha, "교체된 규칙은 더 이상 반영되지 않는다");
        }

        [Test]
        public void DestroyedLayer_IsSkipped()
        {
            var condition = new Condition();
            gate.Register(new[] { new LayerHideRule(condition, new[] { layer }) });
            UnityEngine.Object.DestroyImmediate(layerObject);

            Assert.DoesNotThrow(() => condition.Set(true));
        }

        // 입력 차단 방식(Docs/Refactor/2026-10-26_씬전용.md E-2) - 휴식 진행 중 재화 HUD는 보이되 누를 수 없다.
        [Test]
        public void BlockInput_KeepsAlpha_BlocksRaycast()
        {
            var condition = new Condition();
            gate.Register(new[] { new LayerHideRule(condition, new[] { layer }, LayerHideMode.BlockInput) });
            condition.Set(true);
            Assert.AreEqual(1f, layer.alpha);
            Assert.IsFalse(layer.interactable);
            Assert.IsFalse(layer.blocksRaycasts);

            condition.Set(false);
            Assert.IsTrue(layer.interactable);
            Assert.IsTrue(layer.blocksRaycasts);
        }

        [Test]
        public void LayerHideGate_HideWinsOverBlock()
        {
            var hide = new Condition();
            var block = new Condition();
            gate.Register(new[] { new LayerHideRule(hide, new[] { layer }), new LayerHideRule(block, new[] { layer }, LayerHideMode.BlockInput) });
            block.Set(true);
            hide.Set(true);
            Assert.AreEqual(0f, layer.alpha, "숨김이 우선");
            Assert.IsFalse(layer.blocksRaycasts);
        }

        [Test]
        public void LayerHideGate_HideReleased_BlockRemains()
        {
            var hide = new Condition();
            var block = new Condition();
            gate.Register(new[] { new LayerHideRule(hide, new[] { layer }), new LayerHideRule(block, new[] { layer }, LayerHideMode.BlockInput) });
            hide.Set(true);
            block.Set(true);
            hide.Set(false);
            Assert.AreEqual(1f, layer.alpha);
            Assert.IsFalse(layer.interactable, "입력 차단은 남는다");
            Assert.IsFalse(layer.blocksRaycasts);
        }

        [Test]
        public void UIHideToggle_RaisesChangedOnlyOnChange()
        {
            var toggle = new UIHideToggle();
            var count = 0;
            toggle.Changed += () => count++;
            toggle.Set(true);
            toggle.Set(true);
            toggle.Set(false);
            Assert.AreEqual(2, count);
            Assert.IsFalse(toggle.IsActive);
        }
    }
}
