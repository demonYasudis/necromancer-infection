using UnityEngine;
using static NecromancerInfection.InterfaceTheme;

namespace NecromancerInfection
{
    /// <summary>The main and pause menus share the same settings panel and styling.</summary>
    internal sealed class MenuView
    {
        private bool settingsOpen;
        private GUIStyle title, caption, label, value, button, primary, thumb;

        internal bool CloseSettings()
        {
            if (!settingsOpen) return false;
            settingsOpen = false;
            GameSettings.Save();
            return true;
        }

        internal void Draw(Texture2D backdrop, MainMenu home, GameManager game)
        {
            PrepareStyles();
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            bool enabled = GUI.enabled;
            try
            {
                GUI.depth = -110;
                GUI.matrix = Matrix4x4.identity;
                var screen = new Rect(0, 0, Screen.width, Screen.height);
                GUI.color = Color.white;
                if (backdrop) GUI.DrawTexture(screen, backdrop, ScaleMode.ScaleAndCrop);
                Fill(screen, home ? new Color(.018f, .021f, .015f, .52f) : new Color(.022f, .017f, .014f, .78f));
                if (settingsOpen)
                {
                    var panel = ResultFrame.Begin(358, Purple, 442);
                    DrawSettings(panel);
                }
                else if (home)
                {
                    var panel = ResultFrame.Begin(404, Green, 414, leftAligned: true);
                    DrawHome(panel, home);
                }
                else
                {
                    var panel = ResultFrame.Begin(372, Brass, 414);
                    DrawPause(panel, game);
                }
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = color;
                GUI.depth = depth;
                GUI.enabled = enabled;
            }
        }

        private void PrepareStyles()
        {
            if (title != null) return;
            title = ResultFrame.Label(31, Ink, bold: true);
            caption = ResultFrame.Label(14, Muted);
            label = ResultFrame.Label(17, Ink, TextAnchor.MiddleLeft);
            value = ResultFrame.Label(16, Ink, TextAnchor.MiddleRight, true);
            button = Button(18);
            primary = Button(20, FontStyle.Bold);
            thumb = Button(12);
            thumb.fixedWidth = 17;
            thumb.fixedHeight = 21;
        }

        private void DrawHome(Rect panel, MainMenu home)
        {
            var seal = new Rect(panel.center.x - 26, panel.y + 24, 52, 52);
            Panel(seal, Green);
            HUDIcons.Draw(new Rect(seal.x + 9, seal.y + 9, 34, 34), HUDIcons.SoulVessel, Green);
            GUI.color = Color.white;
            GUI.Label(Row(panel, 88, 81), "NECROMANCER\nINFECTION", title);
            GUI.Label(Row(panel, 173, 26), "ОБУЧЕНИЕ · 4 УРОВНЯ", caption);
            GUI.enabled = !home.IsLoading;
            if (ActionButton(panel, 222, home.IsLoading ? "Загрузка…" : "Играть", true)) home.TryStartGame();
            if (ActionButton(panel, 276, "Настройки")) settingsOpen = true;
            if (ActionButton(panel, 330, "Выход")) home.Quit();
        }

        private void DrawPause(Rect panel, GameManager game)
        {
            GUI.color = Color.white;
            GUI.Label(Row(panel, 26, 43), "ПАУЗА", title);
            GUI.Label(Row(panel, 72, 31), "Esc — продолжить игру", caption);
            GUI.enabled = !game.IsRestarting;
            if (ActionButton(panel, 118, game.IsRestarting ? "Загрузка…" : "Продолжить", true)) game.TryResumeGame();
            if (ActionButton(panel, 172, "Настройки")) settingsOpen = true;
            if (ActionButton(panel, 226, "Начать заново")) game.TryRestartLevel();
            GUI.enabled = !game.IsRestarting && game.CanReturnToMenu;
            if (ActionButton(panel, 280, "В главное меню")) game.TryReturnToMenu();
        }

        private void DrawSettings(Rect panel)
        {
            GUI.color = Color.white;
            GUI.Label(Row(panel, 21, 42), "НАСТРОЙКИ", title);
            float music = Volume(panel, 83, "Громкость музыки", GameSettings.Music, Green);
            if (!Mathf.Approximately(music, GameSettings.Music)) GameSettings.SetMusic(music);
            float effects = Volume(panel, 153, "Громкость звуков", GameSettings.Effects, Purple);
            if (!Mathf.Approximately(effects, GameSettings.Effects)) GameSettings.SetEffects(effects);
            Rect fullscreen = Row(panel, 223, 42);
            GUI.color = Color.white;
            GUI.Label(new Rect(fullscreen.x, fullscreen.y, fullscreen.width - 103, fullscreen.height),
                "Полноэкранный режим", label);
            if (DrawButton(new Rect(fullscreen.xMax - 90, fullscreen.y + 2, 90, 38),
                GameSettings.Fullscreen ? "Вкл." : "Выкл.", button)) GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            if (ActionButton(panel, 290, "Назад", true)) CloseSettings();
        }

        private float Volume(Rect panel, float top, string text, float level, Color accent)
        {
            Rect row = Row(panel, top, 25);
            GUI.color = Color.white;
            GUI.Label(row, text, label);
            GUI.Label(row, $"{Mathf.RoundToInt(level * 100)}%", value);
            var slider = new Rect(row.x, row.yMax + 9, row.width, 21);
            Bar(new Rect(slider.x, slider.y + 7, slider.width, 7), level, accent);
            GUI.color = Color.white;
            return GUI.HorizontalSlider(slider, level, 0, 1, GUIStyle.none, thumb);
        }

        private bool ActionButton(Rect panel, float top, string text, bool emphasis = false)
        {
            var rect = Row(panel, top, 44);
            if (emphasis)
            {
                Panel(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8), Green);
            }
            GUI.color = Color.white;
            return DrawButton(rect, text, emphasis ? primary : button);
        }

        private static Rect Row(Rect panel, float top, float height) =>
            new Rect(panel.x + 24, panel.y + top, panel.width - 48, height);
    }
}
