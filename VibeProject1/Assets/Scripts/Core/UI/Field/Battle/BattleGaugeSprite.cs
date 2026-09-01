using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 체력 게이지바(BattleHealthGaugeView) 전용 둥근 모서리 사각형 스프라이트 - BattlePlaceholderSprite와
    /// 같은 이유로 런타임 1회 생성해 모든 인스턴스(배경+채움)가 공유한다. 채움 바를 "왼쪽 정렬,
    /// 오른쪽부터 깎임"으로 보이게 하는 계산은 스프라이트 피벗이 아니라 BattleHealthGaugeView가
    /// 매 프레임 위치+스케일을 함께 조정하는 방식으로 처리한다(좌측 피벗 스프라이트로 시도했으나
    /// 실제로는 중앙 기준으로 줄어드는 결과가 나와 폐기 - 스프라이트 피벗에 의존하지 않는 쪽이 확실했다).
    /// </summary>
    internal static class BattleGaugeSprite
    {
        // 가로가 넓은 얇은 바 형태 - 모서리가 눈에 띄게 둥글면서도 텍스처가 과하게 크지 않은 값.
        private const int TextureWidth = 64;
        private const int TextureHeight = 16;
        private const float CornerRadius = 6f;

        private static Texture2D texture;
        private static Sprite centered;

        public static Sprite Centered => centered ??= CreateSprite();

        private static Sprite CreateSprite()
        {
            EnsureTexture();
            // PPU=TextureHeight로 둬서 스케일 1일 때 세로 1월드유닛, 가로는 자연히 4배(64/16) 넓은
            // 스프라이트가 된다 - 실제 표시 크기는 BattleHealthGaugeView가 localScale로 다시 맞춘다.
            return Sprite.Create(texture, new Rect(0f, 0f, TextureWidth, TextureHeight), new Vector2(0.5f, 0.5f), TextureHeight);
        }

        private static void EnsureTexture()
        {
            if (texture != null) return;

            texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[TextureWidth * TextureHeight];
            for (var y = 0; y < TextureHeight; y++)
            {
                for (var x = 0; x < TextureWidth; x++)
                {
                    if (IsInsideRoundedRect(x + 0.5f, y + 0.5f))
                    {
                        pixels[y * TextureWidth + x] = Color.white;
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        // 네 모서리의 바깥 사분면에서만 원형 반경 체크를 하고, 그 외(변/중앙)는 그냥 사각형 내부로
        // 취급한다 - 둥근 사각형을 픽셀 단위로 그리는 표준적인 방법.
        private static bool IsInsideRoundedRect(float x, float y)
        {
            var dx = 0f;
            if (x < CornerRadius) dx = CornerRadius - x;
            else if (x > TextureWidth - CornerRadius) dx = x - (TextureWidth - CornerRadius);

            var dy = 0f;
            if (y < CornerRadius) dy = CornerRadius - y;
            else if (y > TextureHeight - CornerRadius) dy = y - (TextureHeight - CornerRadius);

            if (dx <= 0f || dy <= 0f) return true;
            return dx * dx + dy * dy <= CornerRadius * CornerRadius;
        }
    }
}
