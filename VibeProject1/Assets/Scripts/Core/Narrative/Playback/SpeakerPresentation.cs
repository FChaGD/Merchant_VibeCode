using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 화자 표시(설계 88 §5.2). 이름은 캐릭터 테이블·외부 인물 순으로 찾고(조회 함수는 소유자가 넘긴다), 못 찾으면 Id를 그대로 보여 준다 - 빈 이름 칸보다
    /// 대본 오류를 드러내기 쉽다. 색은 Placeholder 스탠딩용으로 Id에서 결정한다(같은 인물 = 같은 색).
    /// </summary>
    public sealed class SpeakerPresentation
    {
        private const float Saturation = 0.45f;
        private const float Brightness = 0.85f;

        private readonly Func<string, string> nameOf;

        public SpeakerPresentation(Func<string, string> nameOf) => this.nameOf = nameOf;

        public string NameOf(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return string.Empty;
            var name = nameOf?.Invoke(speakerId);
            return string.IsNullOrEmpty(name) ? speakerId : name;
        }

        // string.GetHashCode는 실행마다 달라질 수 있어 FNV-1a로 직접 계산한다 - 같은 인물이 상행마다 다른 색이 되지 않게.
        public static Color ColorOf(string speakerId)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in speakerId ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return Color.HSVToRGB(hash % 360 / 360f, Saturation, Brightness);
            }
        }
    }
}
