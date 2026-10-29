using UnityEngine;

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
        private GUIStyle title, text, small, number, right, state, columnTitle;
        private bool initialized;
        public float DisplayedHealth { get; private set; }
        public float DisplayedEnergy { get; private set; }
        public float Health => manager && manager.Necromancer ? manager.Necromancer.CurrentHealth : 0;
        public float Energy => manager && manager.Necromancer ? manager.Necromancer.Energy.Current : 0;
        public bool IsVisible => isActiveAndEnabled && manager && !manager.IsFinished && !manager.IsRestarting;
        public HUDLayout Layout => new HUDLayout(Screen.width, Screen.height);

        private static readonly Color Background = new Color(0.035f, 0.043f, 0.055f, 0.96f);
        private static readonly Color Track = new Color(0.12f, 0.14f, 0.17f);
        private static readonly Color Ink = new Color(0.91f, 0.93f, 0.91f);
        private static readonly Color Muted = new Color(0.64f, 0.69f, 0.73f);
        private static readonly Color Green = new Color(0.5f, 0.87f, 0.36f);
        private static readonly Color Purple = new Color(0.72f, 0.5f, 1);
        private static readonly Color Warning = new Color(1, 0.65f, 0.38f);

        private void Awake() => manager = GetComponent<GameManager>();
        private void Start()
        {
            infection = FindFirstObjectByType<InfectionAbility>();
            protection = FindFirstObjectByType<ProtectionAbility>();
            feedback = GetComponent<GameFeedback>();
            journey = GetComponent<JourneyRoute>();
            assault = manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            SnapValues();
        }

        private void SnapValues()
        {
            DisplayedHealth = Health; DisplayedEnergy = Energy; initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
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
            return IsVisible && layout.Contains(layout.FromScreen(position, Screen.height), notifications && notifications.HasMessage);
        }

        public bool OverlapsWorldLabel(Rect rect, float sourceScale)
        {
            if (!IsVisible) return false;
            var layout = Layout;
            float ratio = sourceScale / layout.Scale;
            var bounds = new Rect(rect.position * ratio, rect.size * ratio);
            var notifications = GetComponent<GameNotifications>();
            return layout.Overlaps(bounds) || (notifications && notifications.OverlapsWorldLabel(bounds));
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            number = new GUIStyle(title) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            right = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
            state = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            columnTitle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            foreach (var style in new[] { title, text, small, number, right, state, columnTitle }) style.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            if (!IsVisible || !initialized) return;
            Styles();
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            try
            {
                var layout = Layout;
                GUI.depth = -20;
                GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1));
                DrawStatus(layout.Status); DrawHorde(layout.Horde);
                DrawObjective(layout.Objective); DrawAbilities(layout.Abilities);
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }

        private void DrawStatus(Rect r)
        {
            Panel(r, Green);
            Label(r, 16, 9, r.width - 32, 25, "НЕКРОМАНТ", title, Ink);
            float maxHP = manager.Necromancer.MaxHealth;
            float maxEnergy = manager.Necromancer.Energy.Maximum;
            bool danger = Health <= maxHP * 0.3f;
            Label(r, r.width - 144, 12, 128, 22, danger ? "КРИТИЧЕСКОЕ HP" : Health < maxHP ? "РАНЕН" : "В СТРОЮ", state, danger ? Warning : Muted);
            Label(r, 16, 40, 100, 22, "Здоровье", text, Ink);
            Label(r, 125, 40, r.width - 141, 22, $"{Health:0.#} / {maxHP:0} HP", right, danger ? Warning : Ink);
            Bar(new Rect(r.x + 16, r.y + 66, r.width - 32, 10), DisplayedHealth / maxHP, danger ? Warning : Green);
            Label(r, 16, 83, 120, 22, "Тёмная энергия", text, Purple);
            Label(r, 140, 83, r.width - 156, 22, $"{Mathf.FloorToInt(Energy)} / {maxEnergy:0}", right, Ink);
            Bar(new Rect(r.x + 16, r.y + 109, r.width - 32, 10), DisplayedEnergy / maxEnergy, Purple);
            bool enough = infection && Energy >= infection.InfectionCost;
            Label(r, 16, 124, r.width - 32, 20, enough ? "Энергия восстанавливается со временем" : "Мало энергии для заражения", small, enough ? Muted : Warning);
        }

        private void DrawHorde(Rect r)
        {
            Panel(r, Green);
            int zombies = feedback ? feedback.ZombieCount : 0;
            int heavies = feedback ? feedback.HeavyCount : 0;
            int skeletons = feedback ? feedback.SkeletonCount : 0;
            Label(r, 16, 9, r.width - 32, 25, $"ОРДА · БОЙЦЫ: {zombies + heavies + skeletons}", title, Ink);
            float column = (r.width - 32) / 3;
            string[] labels = { "Зомби", "Тяжёлые", "Скелеты" };
            int[] counts = { zombies, heavies, skeletons };
            for (int i = 0; i < 3; i++)
            {
                Label(r, 16 + i * column, 45, column, 22, labels[i], columnTitle, Muted);
                Label(r, 16 + i * column, 69, column, 36, counts[i].ToString(), number, i == 1 ? Purple : Green);
            }
            Label(r, 16, 121, r.width - 32, 22, $"С некромантом: {(feedback ? feedback.HordeSize : 1)}", small, Muted);
        }

        private void DrawObjective(Rect r)
        {
            Panel(r, Purple);
            Label(r, 16, 8, r.width - 32, 20, manager.FinalPhase ? "ЦЕЛЬ · ФИНАЛЬНЫЙ ШТУРМ" : "ЦЕЛЬ", small, Purple);
            Label(r, 16, 31, r.width - 32, 26, manager.FinalPhase ? "ОСКВЕРНИТЕ ФЕРМУ" : "ДОБЕРИТЕСЬ ДО ФЕРМЫ", title, Ink);
            if (manager.FinalPhase && assault)
            {
                Label(r, 16, 61, r.width - 32, 22, $"Осквернение: {assault.CorruptionSeconds:0.0} / {assault.CorruptionDuration:0.#} сек", text, Ink);
                Bar(new Rect(r.x + 16, r.y + 91, r.width - 32, 8), assault.Progress, Purple);
                Label(r, 16, 103, r.width - 32, 20, "Защищайте некроманта у фермы", small, Muted);
            }
            else
            {
                Label(r, 16, 60, r.width - 32, 38, journey ? journey.StageHint : "Заражайте жителей и берегите орду для штурма", small, Muted);
                Label(r, 16, 102, r.width - 32, 20, journey ? $"Участок {journey.Stage + 1}/3 · {journey.DistanceTravelled:0}/{journey.Length:0} м" : "Некромант идёт самостоятельно", small, Purple);
            }
        }

        private void DrawAbilities(Rect r)
        {
            Panel(r, Purple);
            if (!infection || !protection || !protection.AreaPrefab) return;
            float column = (r.width - 40) * 0.5f;
            Label(r, 16, 8, column, 24, $"ЛКМ · Заражение  {infection.InfectionCost:0}", text, Energy >= infection.InfectionCost ? Ink : Warning);
            Label(r, 24 + column, 8, column, 24, $"ЛКМ · Поднять тело  {infection.RaiseGuardCost:0}", text, Energy >= infection.RaiseGuardCost ? Ink : Warning);
            Label(r, 16, 39, column, 24, $"ПКМ · Защита  {protection.EnergyCost:0}", text, Energy >= protection.EnergyCost ? Purple : Warning);
            Label(r, 24 + column, 40, column, 29, Energy >= protection.EnergyCost
                ? $"−{protection.AreaPrefab.DamageReduction * 100:0}% урона · {protection.AreaPrefab.Duration:0.#} с · по земле"
                : "Для защиты мало энергии", small, Energy >= protection.EnergyCost ? Muted : Warning);
        }

        private static void Label(Rect r, float x, float y, float width, float height, string value, GUIStyle style, Color color)
        {
            GUI.color = color; GUI.Label(new Rect(r.x + x, r.y + y, width, height), value, style);
        }
        private static void Panel(Rect r, Color accent)
        {
            Fill(r, Background); Fill(new Rect(r.x, r.y, 3, r.height), accent);
        }
        private static void Bar(Rect r, float amount, Color color)
        {
            Fill(r, Track); Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(amount), r.height), color);
        }
        private static void Fill(Rect r, Color color)
        {
            GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture);
        }
    }
}
