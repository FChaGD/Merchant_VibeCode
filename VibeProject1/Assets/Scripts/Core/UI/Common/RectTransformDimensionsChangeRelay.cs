using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// RectTransform 크기 변경(해상도 변경 등)을 이벤트로 중계하는 범용 컴포넌트. 크기에 맞춰 다시 계산할 쪽이
    /// Update 폴링 없이 변경 시점만 받게 한다(Docs/설계/101번 §4.4). 특정 UI 로직을 모른다.
    /// </summary>
    public class RectTransformDimensionsChangeRelay : MonoBehaviour
    {
        public event Action DimensionsChanged;

        private void OnRectTransformDimensionsChange() => DimensionsChanged?.Invoke();
    }
}
