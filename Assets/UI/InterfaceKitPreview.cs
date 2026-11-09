#if UNITY_EDITOR
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Temporary Game View material sheet, excluded from player builds.</summary>
    public sealed class InterfaceKitPreview : MonoBehaviour
    {
        public bool Verified { get; private set; }
        private GUIStyle label, heading, button;

        private void OnGUI()
        {
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            bool enabled = GUI.enabled;
            try
            {
                GUI.depth = -200;
                GUI.matrix = Matrix4x4.identity;
                InterfaceTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), InterfaceTheme.Shadow);
                float scale = Mathf.Min(Screen.width / 960f, Screen.height / 640f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * scale) / 2,
                    (Screen.height - 640 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                if (label == null)
                {
                    label = ResultFrame.Label(16, InterfaceTheme.Ink);
                    heading = ResultFrame.Label(22, InterfaceTheme.Ink, bold: true);
                    button = InterfaceTheme.Button(18, FontStyle.Bold);
                }
                Text(new Rect(20, 12, 920, 40), "КОМПЛЕКТ ИНТЕРФЕЙСА · КОЖА И ЖЕЛЕЗО", heading);
                var small = new Rect(24, 70, 260, 150);
                var wide = new Rect(306, 70, 630, 150);
                InterfaceTheme.Panel(small, InterfaceTheme.Green);
                InterfaceTheme.Panel(wide, InterfaceTheme.Brass);
                Text(new Rect(40, 88, 228, 30), "Панель 260 × 150", label);
                Text(new Rect(324, 88, 594, 30), "Панель 630 × 150 · тот же размер фактуры и уголков", label);
                Text(new Rect(42, 132, 224, 52), "Тёмная кожа\nТекст цвета кости", label);
                Text(new Rect(334, 128, 574, 60), "Рамка растёт по краям.\nЗаклёпки и зерно материала сохраняют пропорции.", label);

                InterfaceTheme.Panel(new Rect(24, 242, 444, 168), InterfaceTheme.Blood);
                Text(new Rect(40, 253, 410, 28), "ПОЛОСЫ · 0% / 50% / 100%", label);
                InterfaceTheme.Bar(new Rect(44, 296, 404, 10), 0, InterfaceTheme.Blood);
                InterfaceTheme.Bar(new Rect(44, 326, 404, 10), .5f, InterfaceTheme.Blood);
                InterfaceTheme.Bar(new Rect(44, 356, 404, 10), 1, InterfaceTheme.Green);
                InterfaceTheme.Bar(new Rect(44, 386, 404, 4), .65f, InterfaceTheme.Green);
                var page = new Rect(490, 242, 212, 168);
                InterfaceTheme.DrawPage(page, false, InterfaceTheme.Purple);
                GUI.color = Color.white;
                GUI.Label(new Rect(506, 280, 180, 90), "Страница\nгримуара", ResultFrame.Label(18, InterfaceTheme.PageInk));
                var selected = new Rect(724, 242, 212, 168);
                InterfaceTheme.DrawPage(selected, true, InterfaceTheme.Purple);
                GUI.color = Color.white;
                GUI.Label(new Rect(740, 280, 180, 90), "Рамка\nвыделения", ResultFrame.Label(18, InterfaceTheme.PageInk));
                string[] states = { "Обычная", "Наведение", "Нажатие", "Недоступна" };
                for (int i = 0; i < 4; i++)
                {
                    var rect = new Rect(24 + i * 234, 472, 212, 48);
                    Text(new Rect(rect.x, 430, rect.width, 30), states[i], label);
                    GUI.color = Color.white;
                    if (i == 3)
                    {
                        GUI.enabled = false;
                        InterfaceTheme.DrawButton(rect, "Повторить", button);
                        GUI.enabled = true;
                    }
                    else if (Event.current.type == EventType.Repaint)
                        button.Draw(rect, new GUIContent("Повторить"), i == 1 || i == 2, i == 2, false, false);
                }
                Text(new Rect(24, 550, 912, 50), "Кириллица: здоровье, тёмная энергия, осквернение.\nБез постоянного свечения и орнамента под текстом.", label);
                if (Event.current.type == EventType.Repaint)
                {
                    Debug.Assert(button.border.left == 4 && button.border.right == 4, "Button fittings must use fixed nine-slice borders");
                    Debug.Assert(button.normal.background != button.hover.background
                        && button.hover.background != button.active.background, "Button states must have distinct faces");
                    Verified = true;
                }
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; GUI.enabled = enabled; }
        }

        private static void Text(Rect rect, string value, GUIStyle style)
        { GUI.color = Color.white; GUI.Label(rect, value, style); }
    }
}
#endif
