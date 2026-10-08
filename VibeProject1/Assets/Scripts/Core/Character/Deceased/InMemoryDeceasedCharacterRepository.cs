using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>사망 기록의 Bootstrap 상주 저장소(Docs/설계/81번 §6.2). 앱 실행 중 유지(기획 80번 §4-5). 두 계약을 각각 등록한다.</summary>
    public class InMemoryDeceasedCharacterRepository : MonoBehaviour, IDeceasedCharacterReader, IDeceasedCharacterRecorder, IManagedComponent
    {
        private readonly HashSet<string> deceasedIds = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IDeceasedCharacterReader>(this);
            registrar.Register<IDeceasedCharacterRecorder>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar) => deceasedIds.Clear();

        public bool IsDeceased(string characterId) => characterId != null && deceasedIds.Contains(characterId);

        public void Record(string characterId)
        {
            if (characterId != null) deceasedIds.Add(characterId);
        }
    }
}
