using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class DefeatScreen : MonoBehaviour
    {
        private GameManager manager;
        private GUIStyle title, message, button, secondary;
        private FinalePresentation finale;
        private void Awake() => manager = GetComponent<GameManager>();

        private void OnGUI()
        {
            if (!manager || !manager.IsDefeated) return;
            if (!finale) finale = GetComponent<FinalePresentation>();
            if (title == null)
            {
                title = ResultFrame.Label(25, InterfaceTheme.Ink, bold: true);
                message = ResultFrame.Label(16, InterfaceTheme.Muted);
                button = InterfaceTheme.Button(20, FontStyle.Bold);
                secondary = InterfaceTheme.Button(16);
            }
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            bool enabled = GUI.enabled;
            try
            {
                GUI.depth = -100;
                GUI.matrix = Matrix4x4.identity;
                InterfaceTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.035f, .008f, .018f,
                    finale ? finale.DefeatFade : .92f));
                Rect panel = ResultFrame.Begin(manager.CanReturnToMenu || manager.IsRestarting ? 365 : 316, InterfaceTheme.Blood);
                ResultFrame.Emblem(panel, false, finale ? finale.ResultAge : 1);
                GUI.Label(new Rect(panel.x + 16, panel.y + 82, panel.width - 32, 68), "НЕКРОМАНТ УНИЧТОЖЕН", title);
                GUI.Label(new Rect(panel.x + 24, panel.y + 158, panel.width - 48, 52),
                    "Посох погас.\nОрда потеряла своего повелителя.", message);
                GUI.enabled = !manager.IsRestarting;
                InterfaceTheme.Panel(new Rect(panel.x + 19, panel.y + 235, panel.width - 38, 56), InterfaceTheme.Brass);
                GUI.color = Color.white;
                if (InterfaceTheme.DrawButton(new Rect(panel.x + 24, panel.y + 240, panel.width - 48, 46),
                    manager.IsRestarting ? "Загрузка…" : "Повторить", button)) manager.TryRestartLevel();
                if (manager.CanReturnToMenu && InterfaceTheme.DrawButton(
                    new Rect(panel.x + 24, panel.y + 304, panel.width - 48, 36), "В главное меню", secondary))
                    manager.TryReturnToMenu();
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = color;
                GUI.depth = depth;
                GUI.enabled = enabled;
            }
        }
    }
}
