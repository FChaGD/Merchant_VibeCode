using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class DependencyManager : MonoBehaviour, IDependencyRegistrar, IManagedComponent
    {
        [Tooltip("IManagedComponent를 구현한 매니저 컴포넌트만 등록한다.")]
        [SerializeField] private List<MonoBehaviour> managedComponents = new();

        private readonly Dictionary<Type, object> registry = new();

        private void Awake()
        {
            foreach (var component in managedComponents)
            {
                if (component is IManagedComponent managed)
                {
                    managed.RegisterSelf(this);
                }
                else if (component != null)
                {
                    Debug.LogWarning($"{component.name}은(는) IManagedComponent를 구현하지 않아 건너뛴다.", component);
                }
            }

            foreach (var component in managedComponents)
            {
                if (component is IManagedComponent managed)
                {
                    managed.ResolveDependencies(this);
                }
            }
        }

        public void Register<T>(T instance) where T : class
        {
            if (registry.ContainsKey(typeof(T)))
            {
                Debug.LogWarning($"{typeof(T).FullName}이(가) 이미 등록되어 있다. 기존 등록을 덮어쓴다 — RegisterSelf 중복 등록 여부를 확인하라.");
            }

            registry[typeof(T)] = instance;
        }

        public T Resolve<T>() where T : class
        {
            if (!registry.TryGetValue(typeof(T), out var instance))
            {
                throw new DependencyNotRegisteredException(typeof(T));
            }

            return (T)instance;
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var value))
            {
                instance = (T)value;
                return true;
            }

            instance = null;
            return false;
        }

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            // DependencyManager 자신이 레지스트라이므로 등록할 인터페이스가 없다.
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // DependencyManager는 다른 매니저에 대한 의존성이 없다.
        }
    }
}
