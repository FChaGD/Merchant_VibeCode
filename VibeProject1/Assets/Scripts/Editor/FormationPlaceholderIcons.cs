using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 배치 UI 팔레트 테스트용 플레이스홀더 아이콘(파랑 사각형/삼각형/원형/오각형/육각형)을 절차적으로
    /// 생성한다. 이미 존재하면 재사용하며, 실제 아트가 준비되면 이 생성 로직 자체를 지워도 된다.
    /// </summary>
    public static class FormationPlaceholderIcons
    {
        private const string Folder = "Assets/Sprites/Formation";
        private static readonly Color32 IconColor = new(60, 130, 246, 255);

        public static Sprite GetOrCreateSquare() => GetOrCreate("Square", DrawSquare);
        public static Sprite GetOrCreateTriangle() => GetOrCreate("Triangle", DrawTriangle);
        public static Sprite GetOrCreateCircle() => GetOrCreate("Circle", DrawCircle);
        public static Sprite GetOrCreatePentagon() => GetOrCreate("Pentagon", texture => DrawRegularPolygon(texture, 5, rotationDegrees: 180f));
        public static Sprite GetOrCreateHexagon() => GetOrCreate("Hexagon", texture => DrawRegularPolygon(texture, 6, rotationDegrees: 90f));

        private static Sprite GetOrCreate(string name, Action<Texture2D> draw)
        {
            var path = $"{Folder}/{name}.png";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder();

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }
            texture.SetPixels32(pixels);

            draw(texture);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            {
                AssetDatabase.CreateFolder("Assets", "Sprites");
            }
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Sprites", "Formation");
            }
        }

        private static void DrawSquare(Texture2D texture)
        {
            var size = texture.width;
            var margin = Mathf.RoundToInt(size * 0.1f);

            for (var y = margin; y < size - margin; y++)
            {
                for (var x = margin; x < size - margin; x++)
                {
                    texture.SetPixel(x, y, IconColor);
                }
            }
        }

        private static void DrawCircle(Texture2D texture)
        {
            var size = texture.width;
            var center = size * 0.5f;
            var radius = size * 0.45f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        texture.SetPixel(x, y, IconColor);
                    }
                }
            }
        }

        private static void DrawTriangle(Texture2D texture)
        {
            var size = texture.width;
            var margin = size * 0.1f;

            var apex = new Vector2(size * 0.5f, size - margin);
            var baseLeft = new Vector2(margin, margin);
            var baseRight = new Vector2(size - margin, margin);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (IsInsideTriangle(p, apex, baseLeft, baseRight))
                    {
                        texture.SetPixel(x, y, IconColor);
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

        /// <summary>
        /// 직업 아이콘(오각형/육각형)용 - 꼭짓점 개수만 다르면 되므로 정다각형 하나로 통일해 그린다.
        /// rotationDegrees는 기본 배치(위쪽 꼭짓점에서 시작, DrawTriangle의 apex-at-top 관례)에서
        /// 시계 방향으로 얼마나 더 돌릴지를 뜻한다.
        /// </summary>
        private static void DrawRegularPolygon(Texture2D texture, int sides, float rotationDegrees = 0f)
        {
            var size = texture.width;
            var center = size * 0.5f;
            var radius = size * 0.45f;
            var rotationRad = rotationDegrees * Mathf.Deg2Rad;

            var vertices = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = -Mathf.PI / 2f + rotationRad + i * (2f * Mathf.PI / sides);
                vertices[i] = new Vector2(center + radius * Mathf.Cos(angle), center + radius * Mathf.Sin(angle));
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (IsInsideConvexPolygon(p, vertices))
                    {
                        texture.SetPixel(x, y, IconColor);
                    }
                }
            }
        }

        private static bool IsInsideConvexPolygon(Vector2 point, Vector2[] vertices)
        {
            // 볼록 다각형 전용 - 모든 변에 대해 점이 같은 방향에 있어야 내부다(정다각형은 항상 볼록).
            var sign = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Length];
                var cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (cross == 0f) continue;

                if (sign == 0f)
                {
                    sign = Mathf.Sign(cross);
                }
                else if (Mathf.Sign(cross) != sign)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
