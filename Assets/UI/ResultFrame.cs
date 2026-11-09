using UnityEngine;

namespace NecromancerInfection
{
    // Shared sizing and decoration for modal panels.
    internal static class ResultFrame
    {
        internal static Rect Begin(float height, Color accent, float maxWidth = 600, bool leftAligned = false)
        {
            float scale = Mathf.Min(1, Screen.height / (height + 32), Screen.width / 320f);
            scale = Mathf.Max(.01f, scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float available = Screen.width / scale;
            float width = Mathf.Min(maxWidth, available - 24);
            float remaining = available - width;
            float x = leftAligned && remaining > 320 ? Mathf.Max(24, remaining * .16f) : remaining / 2;
            var rect = new Rect(x,
                (Screen.height / scale - height) / 2, width, height);
            InterfaceTheme.Panel(rect, accent);
            return rect;
        }

        internal static GUIStyle Label(int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            bool bold = false) => new GUIStyle(GUI.skin.label)
        {
            fontSize = size, alignment = alignment, wordWrap = true,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = color }
        };

        internal static void Emblem(Rect panel, bool victory, float age)
        {
            float x = panel.center.x, y = panel.y + 46;
            InterfaceTheme.Fill(new Rect(x - 36, y - 30, 72, 64), InterfaceTheme.Shadow);
            if (victory)
            {
                // Three tongues of ritual fire rise from the captured farm.
                float glow = 1 + .15f * Mathf.Sin(Mathf.Clamp01(age / .9f) * Mathf.PI);
                for (int i = -1; i <= 1; i++)
                {
                    float h = i == 0 ? 22 : 14;
                    InterfaceTheme.Fill(new Rect(x + i * 16 - 3, y - 27, 6, h), InterfaceTheme.Green * glow);
                }
                GUI.color = InterfaceTheme.Green;
                GUI.DrawTexture(new Rect(x - 25, y - 7, 50, 36), HUDIcons.Farm);
            }
            else
            {
                // An unlit crystal and a broken shaft remain after the necromancer falls.
                InterfaceTheme.Fill(new Rect(x - 3, y - 8, 6, 18), InterfaceTheme.Brass);
                InterfaceTheme.Fill(new Rect(x + 4, y + 14, 6, 16), InterfaceTheme.Iron);
                InterfaceTheme.Fill(new Rect(x - 9, y - 25, 18, 17), InterfaceTheme.Iron);
                InterfaceTheme.Fill(new Rect(x - 5, y - 22, 10, 10), InterfaceTheme.Shadow);
            }
            GUI.color = Color.white;
        }

        internal static void Row(Rect panel, float y, string label, string value, float alpha)
        {
            InterfaceTheme.Fill(new Rect(panel.x + 20, y + 37, panel.width - 40, 1),
                new Color(InterfaceTheme.Iron.r, InterfaceTheme.Iron.g, InterfaceTheme.Iron.b, alpha));
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 126, 37), label,
                Label(16, InterfaceTheme.Muted, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(panel.xMax - 106, y, 86, 37), value,
                Label(21, InterfaceTheme.Ink, TextAnchor.MiddleRight, true));
            GUI.color = Color.white;
        }
    }
}
