using UnityEngine;
using static NecromancerInfection.InterfaceTheme;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class ProgressionScreen : MonoBehaviour
    {
        private SoulProgression progression;
        private GameManager manager;
        private OnboardingGuide guide;
        private GUIStyle title, body, button, heading, description, numbers, rank;
        private GUIStyle guideText, skipButton;
        private static readonly Color PageAccent = new Color32(71, 41, 91, 255);
        private Rect pulseBounds;
        private Color pulseColor;
        private float pulseUntil;
        private const float PulseDuration = .24f;
        private void Awake()
        {
            progression = GetComponent<SoulProgression>();
            guide = GetComponent<OnboardingGuide>();
            manager = GetComponent<GameManager>();
        }
        private void OnGUI()
        {
            bool pulsing = Time.unscaledTime < pulseUntil;
            if (!progression || (manager && (manager.IsPaused || manager.IsFinished || manager.IsRestarting))
                || (!progression.IsChoosing && !pulsing)) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
                body = new GUIStyle(title) { fontSize = 17, fontStyle = FontStyle.Normal };
                button = InterfaceTheme.Button(17, wordWrap: true);
                heading = new GUIStyle(title) { fontSize = 18 };
                description = new GUIStyle(body) { fontSize = 16 };
                numbers = new GUIStyle(title) { fontSize = 25 };
                rank = new GUIStyle(title) { fontSize = 13 };
                guideText = new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
                skipButton = Button(13);
            }
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            try
            {
                GUI.depth = -90;
                if (progression.IsChoosing) DrawCards();
                else
                {
                    // Onboarding banners are hidden in the compact HUD.
                    if (pulsing) DrawPulse();
                }
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
        private void DrawGuide()
        {
            var layout = new HUDLayout(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1));
            Rect r = layout.Warning;
            Panel(r, Brass);
            GUI.color = Brass;
            GUI.DrawTexture(new Rect(r.x + 12, r.y + 12, 24, 24), HUDIcons.Guide);
            GUI.color = Ink;
            GUI.Label(new Rect(r.x + 42, r.y + 4, r.width - 154, r.height - 8), guide.Message, guideText);
            GUI.color = Color.white;
            if (GUI.Button(new Rect(r.xMax - 100, r.y + 8, 90, 32), "Пропустить", skipButton)) guide.Skip();
        }
        public static Rect CardBounds(int index, float width, float height)
        {
            bool vertical = width < 700;
            return vertical ? new Rect(24, 160 + index * 170, width - 48, 154)
                : new Rect((width - 1056) / 2 + index * 356, (height - 310) / 2, 344, 310);
        }
        private void DrawCards()
        {
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.045f, .03f, .025f, .96f));
            bool vertical = Screen.width < Screen.height;
            float scale = vertical ? Mathf.Min(Screen.width / 540f, Screen.height / 720f)
                : Mathf.Min(Screen.width / 1140f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.color = Ink;
            GUI.Label(new Rect(24, 30, width - 48, 52), $"УРОВЕНЬ {progression.Level}", title);
            GUI.Label(new Rect(24, 86, width - 48, 54), "Выберите одно улучшение до конца попытки.\nМир остановлен на время выбора.", body);
            for (int i = 0; i < progression.Offers.Count; i++)
            {
                var upgrade = progression.Offers[i];
                Rect r = CardBounds(i, width, height);
                Color accent = upgrade == SoulProgression.Upgrade.ProtectionCost ? Purple : Green;
                bool hovered = r.Contains(Event.current.mousePosition);
                DrawPage(r, hovered, accent);
                DrawUpgrade(r, vertical, upgrade, accent);
                var action = new Rect(r.x + (vertical ? 105 : 20), r.yMax - 47, r.width - (vertical ? 123 : 40), 33);
                GUI.color = Color.white;
                if (!GUI.Button(action, "Выбрать", button) || !progression.TryChoose(i)) continue;
                pulseBounds = new Rect(r.position * scale, r.size * scale);
                pulseColor = accent;
                pulseUntil = Time.unscaledTime + PulseDuration;
                break;
            }
        }

        private void DrawUpgrade(Rect card, bool vertical, SoulProgression.Upgrade upgrade, Color accent)
        {
            Texture2D icon = upgrade switch
            {
                SoulProgression.Upgrade.EnergyCapacity => HUDIcons.SoulVessel,
                SoulProgression.Upgrade.Regeneration => HUDIcons.DarkSpring,
                SoulProgression.Upgrade.InfectionCost => HUDIcons.Infection,
                _ => HUDIcons.Protection
            };
            var badge = new Rect(vertical ? card.x + 22 : card.center.x - 37, card.y + 20, 74, 74);
            Panel(badge, accent);
            HUDIcons.Draw(new Rect(badge.x + 13, badge.y + 13, 48, 48), icon, accent);
            var rankBounds = vertical ? new Rect(badge.x - 9, badge.yMax + 5, 92, 34)
                : new Rect(card.xMax - 106, card.y + 20, 90, 24);
            GUI.color = PageInk;
            GUI.Label(rankBounds, $"Ранг {progression.Rank(upgrade)} → {progression.Rank(upgrade) + 1}", rank);
            float x = card.x + (vertical ? 105 : 18);
            float width = card.xMax - x - 18;
            GUI.Label(new Rect(x, card.y + (vertical ? 10 : 102), width, vertical ? 34 : 49), SoulProgression.Title(upgrade), heading);
            GUI.Label(new Rect(x, card.y + (vertical ? 44 : 150), width, vertical ? 29 : 40), SoulProgression.Description(upgrade), description);
            GUI.color = PageAccent;
            GUI.Label(new Rect(x, card.y + (vertical ? 73 : 195), width, 30), progression.StatChange(upgrade), numbers);
        }

        private void DrawPulse()
        {
            GUI.matrix = Matrix4x4.identity;
            float strength = Mathf.Clamp01((pulseUntil - Time.unscaledTime) / PulseDuration);
            Color color = pulseColor;
            color.a = strength * .34f;
            Rect rect = pulseBounds;
            rect.size += Vector2.one * (1 - strength) * 24;
            rect.center = pulseBounds.center;
            Fill(rect, color);
            color.a = strength;
            Fill(new Rect(rect.x, rect.y, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), color);
            Fill(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
        }
    }
}
