using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Field 씬에서 전투 월드 오브젝트(캐릭터/보호목표 SpriteRenderer)들의 루트를 찾기 위한 마커
    /// 컴포넌트 - UI Canvas 하위가 아니라 씬 루트에 독립적으로 존재해(Docs/설계/13번 §2, UGUI 좌표계와
    /// 섞이지 않도록) UIElementMarker/SceneUIRoot로는 조회할 수 없다. FieldUIController가
    /// Object.FindFirstObjectByType으로 찾는다.
    /// </summary>
    public class BattleWorldRoot : MonoBehaviour
    {
    }
}
