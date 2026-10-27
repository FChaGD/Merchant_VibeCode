using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 뷰 풀(Docs/Refactor/2026-10-26_씬전용.md A-1, FO-1) - 전투마다 유닛 뷰를 새로 만들고 사망·정리 때 파괴하던 것을 반환·재사용으로 바꿨다.
    /// 대여 때 부모를 바꿔 아군·적 컨테이너가 같은 풀을 쓴다. 반환된 인스턴스의 상태 초기화는 이 클래스가 아니라 뷰의 Unbind가 맡는다 -
    /// 풀은 뷰 종류를 모른다. 파괴된 인스턴스(씬 언로드)는 대여 때 건너뛴다.
    /// </summary>
    internal sealed class ComponentPool<T> where T : Component
    {
        private readonly List<T> active = new();
        private readonly Stack<T> free = new();

        public IReadOnlyList<T> Active => active;

        public T Rent(T prefab, Transform parent)
        {
            T item = null;
            while (free.Count > 0 && item == null) item = free.Pop();
            if (item == null) item = UnityEngine.Object.Instantiate(prefab, parent);
            else item.transform.SetParent(parent, false);

            item.gameObject.SetActive(true);
            active.Add(item);
            return item;
        }

        /// <summary>대여 중이 아닌 인스턴스는 무시한다 - 같은 뷰를 두 번 반환해도 두 번 대여되지 않는다.</summary>
        public void Return(T item)
        {
            if (item == null || !active.Remove(item)) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }

        public void ReturnAll(Action<T> beforeReturn)
        {
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                active.RemoveAt(i);
                if (item == null) continue;
                beforeReturn?.Invoke(item);
                item.gameObject.SetActive(false);
                free.Push(item);
            }
        }
    }
}
