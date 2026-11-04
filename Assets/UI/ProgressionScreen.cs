using UnityEngine;
using static NecromancerInfection.InterfaceTheme;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class ProgressionScreen : MonoBehaviour
    {
        private SoulProgression progression;
        private OnboardingGuide guide;
        private GUIStyle title, body, button, heading, description, numbers, rank;
        private static Texture2D parchment;
        private static readonly Color PageInk = new Color32(49, 31, 23, 255);
        private static readonly Color PageAccent = new Color32(71, 41, 91, 255);
        private Rect pulseBounds;
        private Color pulseColor;
        private float pulseUntil;
        private const float PulseDuration = .24f;
        private void Awake() { progression = GetComponent<SoulProgression>(); guide = GetComponent<OnboardingGuide>(); }
        private void OnGUI()
        {
            bool pulsing = Time.unscaledTime < pulseUntil;
            if (!progression || (!progression.IsChoosing && (!guide || !guide.IsVisible) && !pulsing)) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
                body = new GUIStyle(title) { fontSize = 17, fontStyle = FontStyle.Normal };
                button = InterfaceTheme.Button(17, wordWrap: true);
                heading = new GUIStyle(title) { fontSize = 18 };
                description = new GUIStyle(body) { fontSize = 16 };
                numbers = new GUIStyle(title) { fontSize = 25 };
                rank = new GUIStyle(title) { fontSize = 13 };
            }
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            try
            {
                GUI.depth = -90;
                if (progression.IsChoosing) DrawCards();
                else
                {
                    if (guide && guide.IsVisible) DrawGuide();
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
            InterfaceTheme.Panel(r, InterfaceTheme.Brass);
            GUI.color = Color.white;
            var style = new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(r.x + 10, r.y, r.width - 114, r.height), guide.Message, style);
            if (GUI.Button(new Rect(r.xMax - 100, r.y + 8, 90, 32), "Пропустить")) guide.Skip();
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

        private static void DrawPage(Rect rect, bool hovered, Color accent)
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
            GUI.color = accent;
            GUI.DrawTexture(new Rect(badge.x + 13, badge.y + 13, 48, 48), icon);
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
