using UnityEngine;

namespace Game.Core
{
    /// <summary>대사 출력 수치(기획 86 §4.5, 설계 87 §7.2) - 플레이테스트 후 조정할 값을 한곳에 모은다.</summary>
    public static class DialoguePacing
    {
        public const float CharactersPerSecond = 12f;
        public const float WaitBaseSeconds = 1f;
        public const float WaitPerCharacterSeconds = 0.05f;
        public const float WaitMinSeconds = 2.5f;
        public const float WaitMaxSeconds = 5f;
        public const float DialogueOptionTimeoutSeconds = 10f;

        public static float WaitSecondsFor(int typedCharacters)
            => Mathf.Clamp(WaitBaseSeconds + WaitPerCharacterSeconds * typedCharacters, WaitMinSeconds, WaitMaxSeconds);
    }
}
