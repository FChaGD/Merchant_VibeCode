using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Hub 시설 화면 하나의 생성·등록·해제 단위(Docs/Refactor/2026-10-26_씬전용.md F-1, SS-6·SE-1). Hub 배선이 시설마다 "이전 것 해제 → 의존성 확인 →
    /// 요소 바인딩 → 생성 → 채널 등록" 메서드를 같은 모양으로 반복하던 것을 단위로 뺐다 - 시설 추가 = 바인딩 클래스 1개 + 배선 목록 1줄.
    /// 바인딩은 Bootstrap 상주 배선이 한 번 만들고 Hub 로드마다 Bind한다. 패널이 Bootstrap 상주 저장소 이벤트를 구독하므로 다음 Bind 전에 이전 패널을 해제한다.
    /// 각 바인딩은 자기 선택 의존성을 직접 조회한다 - 배선(Wire)이 의존성 조회기를 받던 기존 방식과 같고, 쓰는 의존성은 각 클래스 요약에 적는다.
    /// </summary>
    internal interface IHubScreenBinding : IDisposable
    {
        void Bind(HubScreenContext context);
    }

    /// <summary>숨김 게이트 규칙을 기여하는 바인딩(숙소 - 휴식 진행 중 재화 HUD 입력 차단). Hub 배선이 모아 한 번에 등록한다(게이트는 등록마다 규칙표를 교체).</summary>
    internal interface IHideRuleSource
    {
        void CollectHideRules(SceneUIRoot sceneUIRoot, List<LayerHideRule> rules);
    }

    /// <summary>Hub 로드마다 바인딩에 넘기는 공용 입력 - 여러 시설이 함께 쓰는 것만 둔다.</summary>
    internal sealed class HubScreenContext
    {
        public HubScreenContext(IDependencyResolver registrar, SceneUIRoot sceneUIRoot, IUIManager uiManager, IPanelRegistrar panels, ITooltipController tooltips,
            IReadOnlyList<InventoryPopupSource> inventorySources, IInputManager input, IGoldItemInfo goldItemInfo, ITripCurrentLocationReader currentLocation, MonoBehaviour runner)
        {
            Registrar = registrar;
            SceneUIRoot = sceneUIRoot;
            UIManager = uiManager;
            Panels = panels;
            Tooltips = tooltips;
            InventorySources = inventorySources;
            Input = input;
            GoldItemInfo = goldItemInfo;
            CurrentLocation = currentLocation;
            Runner = runner;
        }

        public IDependencyResolver Registrar { get; }
        public SceneUIRoot SceneUIRoot { get; }
        public IUIManager UIManager { get; }
        public IPanelRegistrar Panels { get; }
        public ITooltipController Tooltips { get; }
        public IReadOnlyList<InventoryPopupSource> InventorySources { get; }
        public IInputManager Input { get; }
        public IGoldItemInfo GoldItemInfo { get; }
        // ITripCurrentLocationReader는 DI에 읽기 타입으로 등록되지 않아 배선이 저장소 타입으로 조회해 넘긴다(DI 읽기 타입 미등록 규칙).
        public ITripCurrentLocationReader CurrentLocation { get; }
        // plain C# 패널의 코루틴 실행 주체(숙소 휴식 진행).
        public MonoBehaviour Runner { get; }

        /// <summary>선택 의존성 - 없으면 null.</summary>
        public T Optional<T>() where T : class => Registrar.TryResolve<T>(out var instance) ? instance : null;
    }

    /// <summary>패널 하나를 만드는 바인딩의 공통 틀 - Bind마다 이전 패널을 해제하고 Create 결과(등록 못 하면 null)를 보관한다.</summary>
    internal abstract class HubScreenBinding<TPanel> : IHubScreenBinding where TPanel : class, IDisposable
    {
        protected TPanel Panel { get; private set; }

        public void Bind(HubScreenContext context)
        {
            Dispose();
            Panel = Create(context);
        }

        /// <summary>의존성이나 화면 요소가 없으면(인스톨러 미실행) 경고 후 null - 시설 버튼은 "등록되지 않은 패널" 경고만 낸다.</summary>
        protected abstract TPanel Create(HubScreenContext context);

        public virtual void Dispose()
        {
            Panel?.Dispose();
            Panel = null;
        }
    }
}
