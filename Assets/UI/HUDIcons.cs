using UnityEngine;

namespace NecromancerInfection
{
    internal static class HUDIcons
    {
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

        internal static readonly Texture2D Infection = Create(
            "...#....#...",
            "...#.#..#...",
            "...#.#.##...",
            "#..#.#.#....",
            ".#.#.###....",
            "..######....",
            "...#####....",
            "...####.....",
            "....###.....",
            "....###.....",
            "...####.....",
            "............");

        internal static readonly Texture2D Protection = Create(
            ".....##.....",
            "....#..#....",
            "...#....#...",
            "..#..##..#..",
            ".#..#..#..#.",
            "#..#....#..#",
            "#..#....#..#",
            ".#..#..#..#.",
            "..#..##..#..",
            "...#....#...",
            "....#..#....",
            ".....##.....");

        internal static readonly Texture2D Raise = Create(
            "......#.....",
            "....#.#.#...",
            "....#.#.#...",
            "..#.#.###...",
            "...#####....",
            "....####....",
            ".....##.....",
            ".##..##..##.",
            "#########..#",
            "..##..######",
            ".#..##...##.",
            "............");

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
