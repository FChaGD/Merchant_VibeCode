#nullable enable
using System;
using System.Linq;
using UnityEngine;
using Yarn.Unity;

namespace Game.Core
{
    /// <summary>
    /// 잠김 조건 함수(설계 89 §5.3)의 Yarn 컴파일러 선언. Yarn은 대본을 컴파일할 때 함수 이름·타입을 알아야 한다(모르면 컴파일 오류) - 실행 시 구현은
    /// EventEffectCommands가 실행기에 등록하지만 그 등록은 Play 중에만 있으므로, 컴파일용 선언을 여기서 따로 낸다. 이름·타입은 명세 표(EventCommandSpecs,
    /// 설계 89 §5.6)에서 읽는다 - 실행 등록과 같은 원천.
    /// </summary>
    public static class EventYarnFunctionDeclarations
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AddRegistration() => Actions.AddRegistrationMethod(Register);

        private static void Register(IActionRegistration target, RegistrationType registrationType)
        {
            if (registrationType != RegistrationType.Compilation) return;
            foreach (var spec in EventCommandSpecs.Functions)
            {
                target.RegisterFunctionDeclaration(spec.Name, spec.Returns, spec.Parameters.ToArray());
            }
        }
    }
}
