using System;
using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class VictoryScreen : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle title, button, secondary;
        private FinalePresentation finale;
        private void Awake() => manager = GetComponent<GameManager>();

        private void OnGUI()
        {
            if (!manager || !manager.IsVictorious) return;
            if (!finale) finale = GetComponent<FinalePresentation>();
            if (title == null)
            {
                title = ResultFrame.Label(25, InterfaceTheme.Ink, bold: true);
                button = InterfaceTheme.Button(20, FontStyle.Bold);
                secondary = InterfaceTheme.Button(16);
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
                InterfaceTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.015f, .02f, .012f,
                    Mathf.Lerp(.3f, .85f, Mathf.SmoothStep(0, 1, age / .9f))));
                InterfaceTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.42f, 1, .2f,
                    Mathf.Sin(Mathf.Clamp01(age / .45f) * Mathf.PI) * .18f));
                Rect panel = ResultFrame.Begin(492, InterfaceTheme.Green);
                ResultFrame.Emblem(panel, true, age);
                GUI.Label(new Rect(panel.x + 16, panel.y + 82, panel.width - 32, 64), manager.Objective
                    ? manager.Objective.Mode == LevelObjective.ObjectiveMode.Survival ? "ВЫ ВЫЖИЛИ"
                    : manager.Objective.TutorialLevel == 4 ? "ОБУЧЕНИЕ ПРОЙДЕНО" : "УРОВЕНЬ ПРОЙДЕН"
                    : "ФЕРМА ОСКВЕРНЕНА", title);
                var duration = TimeSpan.FromSeconds(Math.Floor(manager.ElapsedSeconds));
                string[] labels = { "Выжившая нежить", "Заражено людей", "Убито стражников", "Время прохождения" };
                string[] values = { manager.SurvivingUndead.ToString(), manager.InfectedCount.ToString(),
                    manager.GuardsKilled.ToString(), $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}" };
                for (int i = 0; i < labels.Length; i++)
                {
                    float alpha = finale ? finale.StatisticAlpha(i) : 1;
                    ResultFrame.Row(panel, panel.y + 151 + i * 40 + (1 - alpha) * 6, labels[i], values[i], alpha);
                }
                GUI.Label(new Rect(panel.x + 20, panel.y + 316, panel.width - 40, 24),
                    "В числе выживших — некромант", ResultFrame.Label(13, InterfaceTheme.Muted));
                GUI.enabled = !manager.IsRestarting;
                InterfaceTheme.Panel(new Rect(panel.x + 19, panel.y + 351, panel.width - 38, 56), InterfaceTheme.Green);
                GUI.color = Color.white;
                if (InterfaceTheme.DrawButton(new Rect(panel.x + 24, panel.y + 356, panel.width - 48, 46),
                    manager.IsRestarting ? "Загрузка…" : manager.CanAdvance ? "Следующий уровень" : "Повторить", button))
                { if (manager.CanAdvance) manager.TryAdvanceLevel(); else manager.TryRestartLevel(); }
                GUI.enabled = !manager.IsRestarting && (manager.CanAdvance || manager.CanReturnToMenu);
                if (InterfaceTheme.DrawButton(new Rect(panel.x + 24, panel.y + 414, panel.width - 48, 32),
                    manager.CanAdvance ? "Повторить" : "В главное меню", secondary))
                { if (manager.CanAdvance) manager.TryRestartLevel(); else manager.TryReturnToMenu(); }
                if (manager.CanAdvance && manager.CanReturnToMenu && InterfaceTheme.DrawButton(
                    new Rect(panel.x + 24, panel.y + 452, panel.width - 48, 30), "В главное меню", secondary))
                    manager.TryReturnToMenu();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.depth = oldDepth;
                GUI.enabled = oldEnabled;
            }
        }
    }
}
