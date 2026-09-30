using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 로딩 작업 목록의 진행(Docs/설계/67번 §4.2). 진행률 = 끝난 작업 수 ÷ 전체 작업 수(사용자 결정 - 단계별 가중치 없음).
    /// 목록은 Reset 때 확정되고 도중에 늘지 않는다.
    /// </summary>
    public sealed class SceneLoadingProgress : ISceneLoadingProgressReader
    {
        private readonly List<string> labels = new();

        public int CompletedCount { get; private set; }
        public int TotalCount => labels.Count;
        public bool IsComplete => CompletedCount >= TotalCount;
        /// <summary>지금 진행 중인 작업 이름. 모두 끝났으면 마지막 작업 이름.</summary>
        public string CurrentLabel => labels.Count == 0 ? string.Empty : labels[Math.Min(CompletedCount, labels.Count - 1)];
        /// <summary>정수 백분율(끝난 수 × 100 / 전체 수). 작업이 없으면 100.</summary>
        public int Percent => TotalCount == 0 ? 100 : CompletedCount * 100 / TotalCount;

        public event Action Changed;

        public void Reset(IEnumerable<string> taskLabels)
        {
            labels.Clear();
            labels.AddRange(taskLabels);
            CompletedCount = 0;
            Changed?.Invoke();
        }

        public void CompleteCurrent()
        {
            if (IsComplete) return;
            CompletedCount++;
            Changed?.Invoke();
        }
    }
}
