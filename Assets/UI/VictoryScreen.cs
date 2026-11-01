using System;
using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class VictoryScreen : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle title;
        private GUIStyle text;
        private GUIStyle button;
        private FinalePresentation finale;

        private void Awake() => manager = GetComponent<GameManager>();

        private void OnGUI()
        {
            if (!manager || !manager.IsVictorious) return;
            if (!finale) finale = GetComponent<FinalePresentation>();
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.83f, 0.65f, 1) }
                };
                text = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.86f, 0.84f, 0.9f) }
                };
                button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldDepth = GUI.depth;
            var oldEnabled = GUI.enabled;
            try
            {
                GUI.depth = -100;
                GUI.matrix = Matrix4x4.identity;
                float age = finale ? finale.ResultAge : 1;
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .012f, .025f,
                    Mathf.Lerp(.3f, .85f, Mathf.SmoothStep(0, 1, age / .9f))));
                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.42f, 1, .2f,
                    Mathf.Sin(Mathf.Clamp01(age / .45f) * Mathf.PI) * .18f));
                float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
                if (scale <= 0) return;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float left = (Screen.width / scale - 600) * 0.5f;
                float top = (Screen.height / scale - 400) * 0.5f;
                Fill(new Rect(left, top, 600, 400), new Color(0.075f, 0.065f, 0.09f));
                Fill(new Rect(left, top, 600, 3), new Color(0.68f, 0.36f, 0.92f));
                GUI.color = Color.white;
                GUI.Label(new Rect(left + 16, top + 24, 568, 50), "ФЕРМА ОСКВЕРНЕНА", title);
                var duration = TimeSpan.FromSeconds(Math.Floor(manager.ElapsedSeconds));
                string time = $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
                string[] rows = { $"Заражено людей: {manager.InfectedCount}", $"Убито стражников: {manager.GuardsKilled}",
                    $"Выжившая нежить: {manager.SurvivingUndead} (с некромантом)", $"Время прохождения: {time}" };
                for (int i = 0; i < rows.Length; i++)
                {
                    float alpha = finale ? finale.StatisticAlpha(i) : 1;
                    GUI.color = new Color(1, 1, 1, alpha);
                    GUI.Label(new Rect(left + 20, top + 95 + i * 36 + (1 - alpha) * 8, 560, 30), rows[i], text);
                }
                GUI.color = Color.white;
                GUI.enabled = !manager.IsRestarting;
                if (GUI.Button(new Rect(left + 40, top + 268, 250, 52),
                    manager.IsRestarting ? "Загрузка…" : "Повторить", button)) manager.TryRestartLevel();
                GUI.enabled = !manager.IsRestarting && manager.CanReturnToMenu;
                if (GUI.Button(new Rect(left + 310, top + 268, 250, 52), "В меню", button)) manager.TryReturnToMenu();
                GUI.enabled = true;
                if (!manager.CanReturnToMenu)
                    GUI.Label(new Rect(left + 20, top + 340, 560, 30), "Меню появится в следующей версии", text);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.depth = oldDepth;
                GUI.enabled = oldEnabled;
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
