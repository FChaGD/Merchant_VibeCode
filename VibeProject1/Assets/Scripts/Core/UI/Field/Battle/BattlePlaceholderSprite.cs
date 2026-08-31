using System;
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
        // 삼각형/원은 Texture2D.whiteTexture 크롭만으로 표현할 수 없어 픽셀을 직접 그려야 한다 -
        // 정렬용이라 128px보다 작아도 충분하다(FormationPlaceholderIcons는 팔레트 아이콘용이라 더 큼).
        private const int ShapeTextureSize = 64;

        private static Sprite whiteSquare;
        private static Sprite whiteTriangle;
        private static Sprite whiteCircle;

        public static Sprite WhiteSquare => whiteSquare ??= Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            Texture2D.whiteTexture.width); // 텍스처 너비=PPU로 둬서 스케일 1일 때 정확히 1x1 월드유닛이 되게 한다.

        public static Sprite WhiteTriangle => whiteTriangle ??= CreateShapeSprite(DrawTriangle);
        public static Sprite WhiteCircle => whiteCircle ??= CreateShapeSprite(DrawCircle);

        // 적 진영 구분(기획 08번 §13.1) - 약탈자/괴수/적대자를 사각형/삼각형/원으로 구분해 실전투에서
        // 육안 식별이 가능하게 한다. 아군/사망 등 SpriteRenderer 틴트는 뷰가 그대로 곱해 적용한다.
        public static Sprite ForEnemyType(EnemyType? type) => type switch
        {
            EnemyType.Marauder => WhiteSquare,
            EnemyType.Monster => WhiteTriangle,
            EnemyType.Adversary => WhiteCircle,
            _ => WhiteSquare,
        };

        // FormationPlaceholderIcons(Editor 전용 asmdef)와 도형 판정 로직이 겹치지만, 이쪽은 런타임에
        // 흰색+알파로 구워야 틴트가 어긋나지 않는다(WhiteSquare 주석 참고) - 에디터/런타임 어셈블리
        // 분리 때문에 공유가 불가능해 그대로 복제했다.
        private static Sprite CreateShapeSprite(Action<Color32[]> draw)
        {
            var texture = new Texture2D(ShapeTextureSize, ShapeTextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[ShapeTextureSize * ShapeTextureSize];
            draw(pixels);
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, ShapeTextureSize, ShapeTextureSize),
                new Vector2(0.5f, 0.5f),
                ShapeTextureSize); // WhiteSquare와 동일하게 텍스처 너비=PPU.
        }

        private static void DrawTriangle(Color32[] pixels)
        {
            var margin = ShapeTextureSize * 0.1f;
            var apex = new Vector2(ShapeTextureSize * 0.5f, ShapeTextureSize - margin);
            var baseLeft = new Vector2(margin, margin);
            var baseRight = new Vector2(ShapeTextureSize - margin, margin);

            for (var y = 0; y < ShapeTextureSize; y++)
            {
                for (var x = 0; x < ShapeTextureSize; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (IsInsideTriangle(p, apex, baseLeft, baseRight))
                    {
                        pixels[y * ShapeTextureSize + x] = Color.white;
                    }
                }
            }
        }

        private static bool IsInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
                (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);

            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;

            return !(hasNeg && hasPos);
        }

        private static void DrawCircle(Color32[] pixels)
        {
            var center = ShapeTextureSize * 0.5f;
            var radius = ShapeTextureSize * 0.45f;

            for (var y = 0; y < ShapeTextureSize; y++)
            {
                for (var x = 0; x < ShapeTextureSize; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        pixels[y * ShapeTextureSize + x] = Color.white;
                    }
                }
            }
        }
    }
}
