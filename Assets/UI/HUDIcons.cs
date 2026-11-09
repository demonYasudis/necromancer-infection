using UnityEngine;

namespace NecromancerInfection
{
    internal static class HUDIcons
    {
        internal static readonly Texture2D Bone = Create(
            "............", ".##......##.", "####....####", ".##########.",
            "..########..", "....####....", "....####....", "..########..",
            ".##########.", "####....####", ".##......##.", "............");

        internal static readonly Texture2D Bell = Create(
            ".....##.....", "....####....", "...######...", "...######...",
            "...######...", "...######...", "..########..", ".##########.",
            "............", ".....##.....", ".....##.....", "............");

        internal static readonly Texture2D Swords = Create(
            ".#........#.", ".##......##.", "..##....##..", "...##..##...",
            "....####....", ".....##.....", "....####....", "...##..##...",
            ".###....###.", "..##....##..", ".#........#.", "............");

        internal static readonly Texture2D Farm = Create(
            ".....##.....", "....####....", "...######...", "..##....##..",
            ".##......##.", "############", "..########..", "..###..###..",
            "..###..###..", "..###..###..", "..###..###..", "............");

        internal static readonly Texture2D Guide = Create(
            "............", ".####..####.", ".#..####..#.", ".#...##...#.",
            ".###.##.###.", ".#...##...#.", ".###.##.###.", ".#...##...#.",
            ".#####.####.", ".....##.....", "............", "............");

        internal static readonly Texture2D SoulVessel = Create(
            "....####....",
            "....####....",
            ".....##.....",
            ".....##.....",
            "...##..##...",
            "..#..##..#..",
            "..#.####.#..",
            "..#..##..#..",
            "..#......#..",
            "...######...",
            "....####....",
            "............");

        internal static readonly Texture2D DarkSpring = Create(
            "......#.....",
            ".....##.....",
            "....####....",
            "....####....",
            ".....##.....",
            "............",
            "..##....##..",
            "##..####..##",
            "............",
            "..##....##..",
            "##..####..##",
            "............");

        internal static readonly Texture2D Infection = CreateAbility(0);
        internal static readonly Texture2D Protection = CreateAbility(1);
        internal static readonly Texture2D Raise = CreateAbility(2);

        internal static readonly Texture2D Peasant = Create(
            "....####....",
            "...######...",
            "..########..",
            "############",
            "....#..#....",
            "....####....",
            ".....##.....",
            "...######...",
            "..########..",
            "..########..",
            "..########..",
            "............");

        internal static readonly Texture2D Knight = Create(
            "....####....",
            "...######...",
            "..########..",
            "..#.#..#.#..",
            "..#.#..#.#..",
            "..########..",
            "...######...",
            "...##..##...",
            "..########..",
            ".##########.",
            ".###.##.###.",
            "............");

        internal static readonly Texture2D Crossbow = Create(
            "...#....#...",
            "..#..##..#..",
            ".#...##...#.",
            "#....##....#",
            "##...##...##",
            ".##..##..##.",
            "..########..",
            ".....##.....",
            ".....##.....",
            ".....##.....",
            "....####....",
            "............");

        internal static void Draw(Rect bounds, Texture2D icon, Color tint, bool unavailable = false)
        {
            bool material = icon == Infection || icon == Protection || icon == Raise;
            GUI.color = material ? (unavailable ? new Color(.58f, .55f, .5f) : Color.white) : tint;
            GUI.DrawTexture(bounds, icon);
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 line = b - a;
            return Vector2.Distance(p, a + line * Mathf.Clamp01(Vector2.Dot(p - a, line) / line.sqrMagnitude)) - radius;
        }

        // Signed shapes are sampled at 96px and filtered down in the HUD. Broad
        // fingers, deep gaps and a single light from the upper left survive small sizes.
        private static float Hand(Vector2 p, bool raised)
        {
            float d = (new Vector2((p.x - 24) / 10, (p.y - 26) / 10).magnitude - 1) * 10;
            d = Mathf.Min(d, Segment(p, new Vector2(24, 30), new Vector2(24, 42), 5));
            d = Mathf.Min(d, Segment(p, new Vector2(17, 28), new Vector2(9, 20), 3));
            for (int i = 0; i < 4; i++)
            {
                float x = 15 + i * 6;
                float top = i == 1 ? 5 : i == 3 ? 13 : 9;
                d = Mathf.Min(d, Segment(p, new Vector2(x, 24), new Vector2(x - 1, top + (raised ? 2 : 0)), 2.1f));
            }
            return d;
        }

        private static Texture2D CreateAbility(int kind)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = kind == 0 ? "Infected palm" : kind == 1 ? "Iron protection rune" : "Hand from earth",
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / 2, (y + .5f) / 2);
                Color pigment;
                float d;
                if (kind == 1)
                {
                    float diamond = (Mathf.Abs(p.x - 24) + Mathf.Abs(p.y - 24) - 18) * .7071f;
                    float frame = Mathf.Abs(diamond) - 2.4f;
                    float rune = Mathf.Min(Segment(p, new Vector2(24, 13), new Vector2(24, 34), 1.8f),
                        Mathf.Min(Segment(p, new Vector2(16, 21), new Vector2(24, 28), 1.8f),
                            Segment(p, new Vector2(32, 21), new Vector2(24, 28), 1.8f)));
                    d = Mathf.Min(frame, rune);
                    pigment = rune < frame ? InterfaceTheme.Purple : new Color32(152, 146, 139, 255);
                }
                else
                {
                    d = Hand(p, kind == 2);
                    pigment = kind == 0 ? new Color32(128, 181, 70, 255) : new Color32(213, 195, 151, 255);
                    if (kind == 2 && p.y > 35)
                    {
                        float earth = Mathf.Abs(p.x - 24) - (21 - Mathf.Abs(p.y - 40) * 2);
                        d = Mathf.Min(d, Mathf.Max(earth, Mathf.Abs(p.y - 40) - 4));
                        pigment = new Color32(126, 91, 57, 255);
                    }
                }
                float coverage = Mathf.Clamp01(.5f - d);
                float light = Mathf.Clamp01(.85f - p.x / 110 - p.y / 140);
                Color ink = Color.Lerp(pigment * .48f, pigment, light);
                if (d > -1.4f) ink = Color.Lerp(InterfaceTheme.Shadow, pigment, light);
                else if (d > -2.5f) ink = Color.Lerp(pigment, InterfaceTheme.Ink, light * .6f);
                ink.a = coverage;
                texture.SetPixel(x, size - y - 1, ink);
            }
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D Create(params string[] rows)
        {
            var texture = new Texture2D(rows[0].Length, rows.Length, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                texture.SetPixel(x, rows.Length - y - 1, rows[y][x] == '#' ? Color.white : Color.clear);
            texture.Apply(false, true);
            return texture;
        }
    }
}
