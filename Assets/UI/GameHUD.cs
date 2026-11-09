using UnityEngine;
using static NecromancerInfection.InterfaceTheme;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class GameHUD : MonoBehaviour
    {
        private GameManager manager;
        private InfectionAbility infection;
        private ProtectionAbility protection;
        private GameFeedback feedback;
        private JourneyRoute journey;
        private FarmAssault assault;
        private AbilityPreview preview;
        private GUIStyle title, text, small, number, right, state, columnTitle;
        private GUIStyle abilityTitle, abilityDetail;
        private bool initialized;
        private float previousHealth, previousEnergy, regenerationClock;
        public float DamagePulse { get; private set; }
        public float SpendPulse { get; private set; }
        public float RestorePulse { get; private set; }
        public float DisplayedHealth { get; private set; }
        public float DisplayedEnergy { get; private set; }
        public float Health => manager && manager.Necromancer ? manager.Necromancer.CurrentHealth : 0;
        public float Energy => manager && manager.Necromancer ? manager.Necromancer.Energy.Current : 0;
        public bool IsVisible => isActiveAndEnabled && manager && !manager.IsFinished && !manager.IsRestarting;
        public HUDLayout Layout => new HUDLayout(Screen.width, Screen.height);

        private static readonly Color Warning = new Color(1, 0.65f, 0.38f);

        private void Awake() => manager = GetComponent<GameManager>();
        private void Start()
        {
            infection = FindFirstObjectByType<InfectionAbility>();
            protection = FindFirstObjectByType<ProtectionAbility>();
            feedback = GetComponent<GameFeedback>();
            preview = GetComponent<AbilityPreview>();
            journey = GetComponent<JourneyRoute>();
            assault = manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            SnapValues();
        }

        private void SnapValues()
        {
            DisplayedHealth = previousHealth = Health;
            DisplayedEnergy = previousEnergy = Energy;
            DamagePulse = SpendPulse = RestorePulse = regenerationClock = 0;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            float dt = Time.unscaledDeltaTime;
            DamagePulse = Mathf.Max(0, DamagePulse - dt / .45f);
            SpendPulse = Mathf.Max(0, SpendPulse - dt / .45f);
            RestorePulse = Mathf.Max(0, RestorePulse - dt / .55f);
            regenerationClock = Mathf.Max(0, regenerationClock - dt);
            if (Health < previousHealth - .001f) DamagePulse = 1;
            float change = Energy - previousEnergy;
            if (change < -.001f)
            {
                SpendPulse = 1; RestorePulse = 0; regenerationClock = 1.5f;
            }
            else if (change > .001f && (change >= 1 || regenerationClock <= 0))
            {
                RestorePulse = 1; SpendPulse = 0; regenerationClock = 2;
            }
            previousHealth = Health; previousEnergy = Energy;
            // Presentation continues to settle while paused; actual values and actions never lag.
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime / 0.14f);
            DisplayedHealth = Smooth(DisplayedHealth, Health, blend);
            DisplayedEnergy = Smooth(DisplayedEnergy, Energy, blend);
        }

        private static float Smooth(float value, float target, float blend) => Mathf.Abs(value - target) < 0.02f
            ? target : Mathf.Lerp(value, target, blend);

        public bool ContainsScreenPosition(Vector2 position)
        {
            var layout = Layout;
            var notifications = GetComponent<GameNotifications>();
            return IsVisible && layout.Contains(layout.FromScreen(position, Screen.height), manager.Objective || (notifications && notifications.HasMessage));
        }

        public bool OverlapsWorldLabel(Rect rect, float sourceScale)
        {
            if (!IsVisible) return false;
            var layout = Layout;
            float ratio = sourceScale / layout.Scale;
            var bounds = new Rect(rect.position * ratio, rect.size * ratio);
            var notifications = GetComponent<GameNotifications>();
            var guide = GetComponent<OnboardingGuide>();
            return layout.Overlaps(bounds, manager.Objective) || (notifications && notifications.OverlapsWorldLabel(bounds));
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            number = new GUIStyle(title) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            right = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
            state = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            columnTitle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            abilityTitle = new GUIStyle(columnTitle) { fontSize = 14, fontStyle = FontStyle.Bold };
            abilityDetail = new GUIStyle(columnTitle) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (var style in new[] { title, text, small, number, right, state, columnTitle, abilityTitle, abilityDetail })
                style.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            if (!IsVisible || !initialized) return;
            if (GetComponent<SoulProgression>() is SoulProgression progression && progression.IsChoosing) return;
            Styles();
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            try
            {
                var layout = Layout;
                GUI.depth = -20;
                GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1));
                DrawStatus(layout);
                var notices = GetComponent<GameNotifications>();
                if (manager.Objective && !(notices && notices.HasMessage))
                {
                    Rect r = layout.Warning;
                    InterfaceTheme.Panel(r, Brass);
                    GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 20, 20), manager.Objective.Title, title);
                    GUI.Label(new Rect(r.x + 10, r.y + 23, r.width - 20, 24), manager.Objective.Hint, small);
                }
                // The permanent objective card is intentionally hidden.
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }

        private void DrawStatus(HUDLayout layout)
        {
            ResourceVessel.Draw(layout.Status, DisplayedHealth / manager.Necromancer.MaxHealth,
                new Color(.85f, .045f, .075f), DamagePulse);
            ResourceVessel.Draw(layout.EnergyOrb, DisplayedEnergy / manager.Necromancer.Energy.Maximum,
                new Color(.32f, .85f, .08f), Mathf.Max(SpendPulse, RestorePulse));
            var progression = GetComponent<SoulProgression>();
            Bar(layout.Experience, progression ? (float)progression.Experience / progression.RequiredExperience : 0, Brass);
        }

        private void DrawObjective(Rect r)
        {
            DrawPage(r, false, manager.FinalPhase ? Purple : Brass);
            GUI.color = PageInk;
            GUI.DrawTexture(new Rect(r.xMax - 38, r.y + 12, 22, 22), HUDIcons.Farm);
            Label(r, 16, 8, r.width - 54, 20, manager.FinalPhase ? "ЦЕЛЬ · ФИНАЛЬНЫЙ ШТУРМ" : "ЦЕЛЬ", small, PageInk);
            Label(r, 16, 31, r.width - 32, 26, manager.FinalPhase ? "ОСКВЕРНИТЕ ФЕРМУ" : "ДОБЕРИТЕСЬ ДО ФЕРМЫ", title, PageInk);
            if (manager.FinalPhase && assault)
            {
                Label(r, 16, 61, r.width - 32, 22, $"Осквернение: {assault.CorruptionSeconds:0.0} / {assault.CorruptionDuration:0.#} сек", text, PageInk);
                Bar(new Rect(r.x + 16, r.y + 91, r.width - 32, 8), assault.Progress, Purple);
                Label(r, 16, 103, r.width - 32, 20, "Защищайте некроманта у фермы", small, PageInk);
            }
            else
            {
                Label(r, 16, 60, r.width - 32, 38, journey ? journey.StageHint : "Заражайте жителей и берегите орду для штурма", small, PageInk);
                Label(r, 16, 102, r.width - 32, 20, journey ? $"Участок {journey.Stage + 1}/3 · {journey.DistanceTravelled:0}/{journey.Length:0} м" : "Некромант идёт самостоятельно", small, PageInk);
            }
        }

        private static void Label(Rect r, float x, float y, float width, float height, string value, GUIStyle style, Color color)
        {
            GUI.color = color; GUI.Label(new Rect(r.x + x, r.y + y, width, height), value, style);
        }
    }
}
