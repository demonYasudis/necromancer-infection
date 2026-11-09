using UnityEngine;

namespace NecromancerInfection
{
    internal static class InterfaceTheme
    {
        internal const int Inset = 16;
        internal static readonly Color Leather = new Color32(33, 28, 24, 250);
        internal static readonly Color Shadow = new Color32(16, 15, 13, 255);
        internal static readonly Color Iron = new Color32(73, 67, 59, 255);
        internal static readonly Color Brass = new Color32(146, 117, 74, 255);
        internal static readonly Color Ink = new Color32(233, 221, 194, 255);
        internal static readonly Color Muted = new Color32(188, 174, 150, 255);
        internal static readonly Color Blood = new Color32(145, 45, 50, 255);
        internal static readonly Color Green = new Color32(131, 190, 70, 255);
        internal static readonly Color Purple = new Color32(167, 117, 219, 255);
        internal static readonly Color PageInk = new Color32(49, 31, 23, 255);
        private static Texture2D normal, hover, pressed, disabled, parchment, leatherGrain, ironGrain;

        internal static void Panel(Rect rect, Color accent)
        {
            Fill(rect, Shadow);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Iron);
            if (!ironGrain) ironGrain = CreateGrain(false);
            Tile(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), ironGrain);
            var interior = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
            Fill(interior, Leather);
            if (!leatherGrain) leatherGrain = CreateGrain(true);
            Tile(interior, leatherGrain);
            // Fixed-width bevels and corner fittings, independent of panel dimensions.
            Fill(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 1), new Color(0, 0, 0, .5f));
            Fill(new Rect(rect.x + 4, rect.yMax - 5, rect.width - 8, 1), new Color(.6f, .5f, .35f, .16f));
            Fill(new Rect(rect.x + Inset, rect.y + 3, rect.width - 2 * Inset, 1), accent);
            Corner(rect.x + 2, rect.y + 2, 1, 1);
            Corner(rect.xMax - 2, rect.y + 2, -1, 1);
            Corner(rect.x + 2, rect.yMax - 2, 1, -1);
            Corner(rect.xMax - 2, rect.yMax - 2, -1, -1);
        }

        private static void Corner(float x, float y, int horizontal, int vertical)
        {
            Fill(new Rect(x - (horizontal < 0 ? 10 : 0), y - (vertical < 0 ? 2 : 0), 10, 2), Brass);
            Fill(new Rect(x - (horizontal < 0 ? 2 : 0), y - (vertical < 0 ? 10 : 0), 2, 10), Brass);
            Fill(new Rect(x + (horizontal < 0 ? -6 : 4), y + (vertical < 0 ? -6 : 4), 2, 2), Brass);
            Fill(new Rect(x + (horizontal < 0 ? -6 : 4), y + (vertical < 0 ? -6 : 4), 1, 1), Muted);
        }

        internal static void Selection(Rect rect, Color accent)
        {
            // An inset outline never grows over adjacent labels or changes hit testing.
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, 1), accent);
            Fill(new Rect(rect.x + 1, rect.yMax - 2, rect.width - 2, 1), accent);
            Fill(new Rect(rect.x + 1, rect.y + 1, 1, rect.height - 2), accent);
            Fill(new Rect(rect.xMax - 2, rect.y + 1, 1, rect.height - 2), accent);
        }

        internal static void Bar(Rect rect, float amount, Color color)
        {
            Fill(rect, Iron);
            var track = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            Fill(track, Shadow);
            track.width *= Mathf.Clamp01(amount);
            Fill(track, color);
            if (track.height >= 5 && track.width > 1)
            {
                Fill(new Rect(track.x, track.y, track.width, 1), Color.Lerp(color, Ink, .3f));
                Fill(new Rect(track.x, track.yMax - 1, track.width, 1), Color.Lerp(color, Shadow, .45f));
            }
        }

        internal static void ResourceBar(Rect rect, float amount, bool energy, float pulse, bool spending)
        {
            Color pigment = energy ? Green : Blood;
            Color flash = spending ? Brass : energy ? Ink : new Color32(223, 119, 112, 255);
            var rail = new Rect(rect.x + 5, rect.y, rect.width - 10, rect.height);
            Fill(new Rect(rail.x - 2, rail.y - 2, rail.width + 4, rail.height + 4), Shadow);
            Bar(rail, amount, Color.Lerp(pigment, flash, pulse * .45f));
            // Fixed iron claws and brass tips echo the staff without stretching the fittings.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? rect.x : rect.xMax - 5;
                Fill(new Rect(x, rect.y - 2, 5, rect.height + 4), Iron);
                Fill(new Rect(x + 1, rect.y - 3, 3, 3), Brass);
                Fill(new Rect(x + 1, rect.yMax, 3, 3), Brass);
            }
            if (energy)
            {
                Fill(new Rect(rect.x, rect.y + 2, 5, rect.height - 4), Color.Lerp(Green, Ink, .25f + pulse * .5f));
                Fill(new Rect(rect.x + 2, rect.y, 1, rect.height), Green);
                if (pulse > 0 && !spending && amount > 0)
                {
                    float x = rail.x + Mathf.Clamp01(amount) * (rail.width - 2) * (1 - pulse);
                    Fill(new Rect(x, rail.y + 2, 2, rail.height - 4), new Color(1, 1, .8f, pulse * .65f));
                }
            }
            if (pulse > 0)
                Fill(new Rect(rail.x, rail.yMax + 1, rail.width * Mathf.Clamp01(amount), 1),
                    new Color(flash.r, flash.g, flash.b, pulse * .65f));
        }

        internal static GUIStyle Button(int size, FontStyle fontStyle = FontStyle.Normal, bool wordWrap = false)
        {
            if (!normal)
            {
                normal = ButtonFace(Leather, Brass);
                hover = ButtonFace(new Color32(65, 49, 31, 255), Ink);
                pressed = ButtonFace(Shadow, Green, true);
                disabled = ButtonFace(Shadow, Iron);
            }
            var style = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(4, 4, 4, 4),
                padding = new RectOffset(7, 7, 3, 3),
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = wordWrap
            };
            style.normal.background = normal;
            style.hover.background = style.focused.background = hover;
            style.active.background = pressed;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = Ink;
            return style;
        }

        internal static bool DrawButton(Rect rect, string label, GUIStyle style)
        {
            if (GUI.enabled) return GUI.Button(rect, label, style);
            if (Event.current.type == EventType.Repaint)
            {
                var color = GUI.color;
                GUI.color = Color.white;
                var disabledStyle = new GUIStyle(style);
                disabledStyle.normal.background = disabled;
                disabledStyle.Draw(rect, GUIContent.none, false, false, false, false);
                GUI.color = Muted;
                GUI.Label(rect, label, new GUIStyle(GUI.skin.label)
                {
                    alignment = style.alignment, fontSize = style.fontSize, fontStyle = style.fontStyle,
                    wordWrap = style.wordWrap, normal = { textColor = Color.white }
                });
                GUI.color = color;
            }
            return false;
        }

        private static Texture2D ButtonFace(Color fill, Color rim, bool sunken = false)
        {
            // Nine-slice fittings; the sole stretched centre pixel is a flat material colour.
            const int size = 9;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y);
                Color pixel = edge == 0 ? Shadow : edge == 1 ? rim : fill;
                if (edge == 2)
                    pixel = Color.Lerp(fill, ((y > size / 2) != sunken) ? Ink : Shadow, .18f);
                texture.SetPixel(x, y, pixel);
            }
            texture.Apply(false, true);
            return texture;
        }

        internal static void DrawPage(Rect rect, bool hovered, Color accent)
        {
            Panel(rect, hovered ? accent : Brass);
            var page = new Rect(rect.x + 7, rect.y + 7, rect.width - 14, rect.height - 14);
            Fill(page, hovered ? new Color32(235, 207, 152, 255) : new Color32(215, 188, 139, 255));
            if (!parchment) parchment = CreateParchment();
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(page, parchment, new Rect(0, 0, page.width / 96, page.height / 96));
            Color edge = new Color(.34f, .19f, .10f, .42f);
            Fill(new Rect(page.x, page.y, page.width, 2), edge);
            Fill(new Rect(page.x, page.yMax - 3, page.width, 3), edge);
            Fill(new Rect(page.x, page.y, 4, page.height), edge);
            Fill(new Rect(page.xMax - 2, page.y, 2, page.height), edge);
            if (hovered) Selection(rect, accent);
        }

        private static void Tile(Rect rect, Texture2D texture)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, texture,
                new Rect(0, 0, rect.width / texture.width, rect.height / texture.height));
        }

        private static Texture2D CreateGrain(bool leather)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = leather ? "UI leather grain" : "UI worn iron grain",
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var random = new System.Random(leather ? 417 : 928);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool light = random.Next(3) == 0;
                int alpha = random.Next(leather ? 10 : 22);
                if (leather && (x + y * 3) % 23 == 0) alpha += 12;
                pixels[y * size + x] = light ? new Color32(189, 161, 123, (byte)alpha)
                    : new Color32(0, 0, 0, (byte)(alpha * 2));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateParchment()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };
            var random = new System.Random(163);
            var grain = new Color32[size * size];
            for (int i = 0; i < grain.Length; i++)
                grain[i] = new Color32(78, 43, 27, (byte)(random.Next(28) + (random.Next(16) == 0 ? 28 : 0)));
            texture.SetPixels32(grain);
            texture.Apply(false, true);
            return texture;
        }

        internal static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
