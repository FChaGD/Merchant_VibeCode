using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 3개 EnemyRecognitionType 구현체가 공유하는 저장 골격 - 한 번 인식된 적은 계속 인식 상태를
    /// 유지한다(다시 못 보게 되는 경우가 없어 "인식 취소"는 다루지 않는다). 개별 판정 조건만
    /// ShouldRecognize로 하위 클래스가 다르게 구현한다. 반환 리스트(recognizedBuffer)는 매 틱
    /// 재사용한다 - 재할당을 피하기 위한 최적화 결정이다(Docs/설계/12번 §7 점검 이력).
    /// </summary>
    public abstract class EnemyRecognitionTrackerBase : IEnemyRecognitionTracker
    {
        private readonly Dictionary<IDamageable, float> elapsedSecondsByEnemy = new();
        private readonly HashSet<IDamageable> recognized = new();
        private readonly List<IDamageable> recognizedBuffer = new();

        public IReadOnlyList<IDamageable> TickAndGetRecognized(float deltaTime, IReadOnlyList<IDamageable> allEnemies, IActivityRadiusZone radiusZone)
        {
            recognizedBuffer.Clear();

            foreach (var enemy in allEnemies)
            {
                if (!enemy.IsAlive) continue;

                if (!recognized.Contains(enemy))
                {
                    elapsedSecondsByEnemy.TryGetValue(enemy, out var elapsed);
                    elapsed += deltaTime;
                    elapsedSecondsByEnemy[enemy] = elapsed;

                    if (ShouldRecognize(enemy, elapsed, radiusZone))
                    {
                        recognized.Add(enemy);
                    }
                }

                if (recognized.Contains(enemy))
                {
                    recognizedBuffer.Add(enemy);
                }
            }

            return recognizedBuffer;
        }

        public void NotifyAttackedBy(IBattleCombatant attacker)
        {
            recognized.Add(attacker);
        }

        protected abstract bool ShouldRecognize(IDamageable enemy, float elapsedSeconds, IActivityRadiusZone radiusZone);
    }
}
