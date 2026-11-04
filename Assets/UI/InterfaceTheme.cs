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
        private static Texture2D normal, hover, pressed, disabled;

        internal static void Panel(Rect rect, Color accent)
        {
            Fill(rect, Shadow);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Iron);
            Fill(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8), Leather);
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
        }

        internal static void Bar(Rect rect, float amount, Color color)
        {
            Fill(rect, Iron);
            var track = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            Fill(track, Shadow);
            track.width *= Mathf.Clamp01(amount);
            Fill(track, color);
        }

        internal static GUIStyle Button(int size, FontStyle fontStyle = FontStyle.Normal, bool wordWrap = false)
        {
            if (!normal)
            {
                normal = ButtonFace(Leather, Brass);
                hover = ButtonFace(new Color32(65, 49, 31, 255), Ink);
                pressed = ButtonFace(Shadow, Green);
                disabled = ButtonFace(Shadow, Iron);
            }
            var style = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(2, 2, 2, 2),
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
                GUI.DrawTexture(rect, disabled);
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

        private static Texture2D ButtonFace(Color fill, Color rim)
        {
            const int size = 5;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, x == 0 || x == size - 1 || y == 0 || y == size - 1 ? rim : fill);
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
