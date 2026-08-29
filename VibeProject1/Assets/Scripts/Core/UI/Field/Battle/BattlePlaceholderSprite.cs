using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// SpriteRenderer 틴트(색상 곱연산)로 아군/적/사망 등 색을 구분하려면 흰색 스프라이트가 필요하다
    /// (FormationPlaceholderIcons는 파란색으로 고정 구워져 있어 틴트가 어긋나 재사용 불가, Docs/설계/
    /// 13번 §3). 에셋 파일 없이 런타임에 1회 생성해 모든 인스턴스가 공유한다(Sprite는 여러
    /// SpriteRenderer가 동시에 참조해도 안전 - Texture2D.whiteTexture 기반이라 별도 텍스처 생성도
    /// 없음). 실제 아트가 준비되면 이 파일 자체를 삭제하고 각 View의 프리팹에 스프라이트를 직접
    /// 연결하면 된다(Placeholder 컨벤션).
    /// </summary>
    internal static class BattlePlaceholderSprite
    {
        private static Sprite whiteSquare;

        public static Sprite WhiteSquare => whiteSquare ??= Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            Texture2D.whiteTexture.width); // 텍스처 너비=PPU로 둬서 스케일 1일 때 정확히 1x1 월드유닛이 되게 한다.
    }
}
