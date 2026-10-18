using System;
using System.Collections;

namespace Game.Core
{
    /// <summary>이벤트 진행자가 효과 명령에 주는 진행 수단 - 결과 줄 대기·정리 화면·전투 요청.</summary>
    public interface IEventEffectHost
    {
        /// <summary>이벤트가 진행 중인지 - 도주·패배로 끝나면 대기 중인 명령이 빠져나온다.</summary>
        bool IsRunning { get; }
        void PresentResult(string text, Action onDone);
        bool TryShowCargoRecovery(Action onDone);
        /// <summary>전투 요청. 승리로 돌아오면 onVictory - 도주·패배는 진행자가 이벤트를 끝낸다.</summary>
        void RequestBattle(Action onVictory);
    }

    /// <summary>효과 단위 묶음 - 전부 선택적이다(없으면 그 명령만 경고 후 건너뜀).</summary>
    public sealed class EventEffectServices
    {
        public IGoldEventAdjuster Gold;
        public EventCargoGoods Goods;
        public EventRosterEffects Roster;
        public ISessionState Session;
        public ITripClockAdjuster Clock;
        public EventRoadDifficultyEffects Road;
        public Action<string> Warn = _ => { };
    }

    /// <summary>
    /// 효과 명령·잠김 조건 함수(설계 89 §5.1·§5.3)의 Yarn 등록과 실행. 명령은 IEnumerator를 돌려 Yarn이 끝날 때까지 기다리게 한다(IEnumerator 반환 =
    /// 코루틴 명령) - 효과 적용 → 결과 줄 → 클릭을 한 명령 안에서 끝내야 대본 순서와 화면이 어긋나지 않는다. 실행 불가(없는 Id·미연결 서비스·잘못된 인자)는
    /// 경고 후 건너뛰고 대본은 계속한다. 대기는 이벤트가 끝나면(도주·패배) 빠져나온다 - 남은 코루틴이 다음 이벤트를 기다리지 않게.
    /// 이름·인자 형식은 EventCommandSpecs와 같아야 한다(EditMode 테스트가 확인).
    /// </summary>
    public sealed class EventEffectCommands
    {
        private readonly EventEffectServices services;
        private readonly IEventEffectHost host;

        public EventEffectCommands(EventEffectServices services, IEventEffectHost host)
        {
            this.services = services;
            this.host = host;
        }

        public void Register(IDialogueCommandRegistry registry)
        {
            registry.AddCommand(EventCommandSpecs.Battle, (Func<IEnumerator>)Battle);
            registry.AddCommand(EventCommandSpecs.Gold, (Func<int, IEnumerator>)Gold);
            registry.AddCommand(EventCommandSpecs.GoodsAdd, (Func<string, int, IEnumerator>)GoodsAdd);
            registry.AddCommand(EventCommandSpecs.GoodsRemove, (Func<string, int, IEnumerator>)GoodsRemove);
            registry.AddCommand(EventCommandSpecs.Delay, (Func<float, IEnumerator>)Delay);
            registry.AddCommand(EventCommandSpecs.Difficulty, (Func<int, string, int, IEnumerator>)Difficulty);
            registry.AddCommand(EventCommandSpecs.Join, (Func<string, IEnumerator>)Join);
            registry.AddCommand(EventCommandSpecs.Leave, (Func<string, IEnumerator>)Leave);
            registry.AddFunction(EventCommandSpecs.GoldFunction, (Func<int>)OwnedGold);
            registry.AddFunction(EventCommandSpecs.GoodsFunction, (Func<string, int>)GoodsCount);
            registry.AddFunction(EventCommandSpecs.HiredFunction, (Func<string, bool>)IsHired);
        }

        public IEnumerator Battle()
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            var won = false;
            host.RequestBattle(() => won = true);
            while (!won && host.IsRunning) yield return null;
        }

        public IEnumerator Gold(int amount)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Gold == null)
            {
                services.Warn("<<gold>>: 골드 서비스가 없다 - 건너뜀");
                yield break;
            }
            if (amount == 0) yield break;
            var actual = amount > 0 ? services.Gold.Gain(amount) : -services.Gold.LoseUpTo(-amount);
            // 잃는 효과인데 가진 골드가 없으면 「골드 +0」이 아니라 없음 줄(설계 89 §5.1, 2026-10-16 검진).
            yield return Show(amount < 0 && actual == 0 ? EventEffectFormat.NoGoldLost : EventEffectFormat.Gold(actual));
        }

        public IEnumerator GoodsAdd(string itemId, int count)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Goods == null || count <= 0)
            {
                services.Warn($"<<goods_add {itemId} {count}>>: 실행 불가 - 건너뜀");
                yield break;
            }
            if (!services.Goods.TryAdd(itemId, count, out var name, out var staged))
            {
                services.Warn($"<<goods_add>>: 없는 품목 '{itemId}' - 건너뜀");
                yield break;
            }
            // 결과 줄을 먼저 보인다 - 정리 화면에서 버린 뒤에 획득 문구가 뜨면 부자연스럽다(설계 89 §5.1, 2026-10-16 보완 검진).
            yield return Show(EventEffectFormat.GoodsGained(name, count));
            if (staged <= 0 || !host.IsRunning) yield break;
            var done = false;
            if (host.TryShowCargoRecovery(() => done = true))
            {
                while (!done && host.IsRunning) yield return null;
            }
            else
            {
                services.Warn("<<goods_add>>: 정리 화면이 없어 넘친 무역품이 임시 보관에 남는다(Tools > Game > Build Field Scene).");
            }
        }

        public IEnumerator GoodsRemove(string target, int count)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Goods == null || count <= 0)
            {
                services.Warn($"<<goods_remove {target} {count}>>: 실행 불가 - 건너뜀");
                yield break;
            }
            if (target == EventCargoGoods.RandomTarget)
            {
                yield return Show(EventEffectFormat.GoodsLost(services.Goods.RemoveRandom(count)));
                yield break;
            }
            if (!services.Goods.TryRemove(target, count, out var name, out var removed))
            {
                services.Warn($"<<goods_remove>>: 없는 품목 '{target}' - 건너뜀");
                yield break;
            }
            yield return Show(removed > 0 ? EventEffectFormat.GoodsLost(name, removed) : EventEffectFormat.GoodsNoneLost(name));
        }

        public IEnumerator Delay(float seconds)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (seconds > 0f)
            {
                if (services.Session == null)
                {
                    services.Warn("<<delay>>: 상행 시계가 없다 - 건너뜀");
                    yield break;
                }
                services.Session.ExtendDuration(seconds);
                yield return Show(EventEffectFormat.Delayed(seconds));
            }
            else if (seconds < 0f)
            {
                if (services.Clock == null)
                {
                    services.Warn("<<delay>>: 상행 시계 조정이 없다 - 건너뜀");
                    yield break;
                }
                yield return Show(EventEffectFormat.Shortened(services.Clock.ShortenDuration(-seconds)));
            }
        }

        public IEnumerator Difficulty(int change, string mode, int days = 0)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Road == null || !services.Road.TryApply(change, mode, days, out var text))
            {
                services.Warn($"<<difficulty {change} {mode} {days}>>: 실행 불가(진행 중 구간 없음·잘못된 방식) - 건너뜀");
                yield break;
            }
            yield return Show(text);
        }

        public IEnumerator Join(string characterId)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Roster == null)
            {
                services.Warn("<<join>>: 고용 명단이 없다 - 건너뜀");
                yield break;
            }
            var result = services.Roster.Join(characterId);
            if (result != RosterEffectResult.Done)
            {
                services.Warn($"<<join {characterId}>>: {result} - 건너뜀");
                yield break;
            }
            yield return Show(EventEffectFormat.Joined(services.Roster.NameOf(characterId)));
        }

        public IEnumerator Leave(string characterId)
        {
            if (!host.IsRunning) yield break;   // 도주·패배로 끝난 이벤트의 흘려보내는 대본 - 효과 없음
            if (services.Roster == null)
            {
                services.Warn("<<leave>>: 고용 명단이 없다 - 건너뜀");
                yield break;
            }
            var name = services.Roster.NameOf(characterId);
            var result = services.Roster.Leave(characterId);
            if (result != RosterEffectResult.Done)
            {
                services.Warn($"<<leave {characterId}>>: {result} - 건너뜀");
                yield break;
            }
            yield return Show(EventEffectFormat.Left(name));
        }

        public int OwnedGold() => services.Gold?.OwnedGold ?? 0;
        public int GoodsCount(string itemId) => services.Goods?.CountOf(itemId) ?? 0;
        public bool IsHired(string characterId) => services.Roster != null && services.Roster.IsHired(characterId);

        private IEnumerator Show(string text)
        {
            if (string.IsNullOrEmpty(text) || !host.IsRunning) yield break;
            var done = false;
            host.PresentResult(text, () => done = true);
            while (!done && host.IsRunning) yield return null;
        }
    }
}
