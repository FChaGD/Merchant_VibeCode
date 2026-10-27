using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 평판 수치(기획 23 §3.2·기획 106 §4.4, Docs/설계/107번 §10). 중립 0에서 시작하는 양방향 수치이고 소모되지 않는다. 효과(가격·인카운터)는 아직
    /// 정하지 않아 수치만 둔다. 플레이 세션 동안만 유지(저장 시스템 후속).
    /// </summary>
    public class ReputationManager : MonoBehaviour, IReputationAdjuster, IManagedComponent
    {
        [SerializeField] private int min = -100;
        [SerializeField] private int max = 100;

        private int value;

        public int Value => value;
        public int Min => min;
        public int Max => max;
        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IReputationAdjuster>(this);
            registrar.Register<IReputationReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar) { }

        public int Adjust(int delta)
        {
            var next = Mathf.Clamp(value + delta, min, max);
            var actual = next - value;
            if (actual == 0) return 0;
            value = next;
            Changed?.Invoke();
            return actual;
        }
    }
}
