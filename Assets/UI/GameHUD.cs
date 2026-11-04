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
            var guide = GetComponent<OnboardingGuide>();
            return IsVisible && layout.Contains(layout.FromScreen(position, Screen.height), (notifications && notifications.HasMessage) || (guide && guide.IsVisible));
        }

        public bool OverlapsWorldLabel(Rect rect, float sourceScale)
        {
            if (!IsVisible) return false;
            var layout = Layout;
            float ratio = sourceScale / layout.Scale;
            var bounds = new Rect(rect.position * ratio, rect.size * ratio);
            var notifications = GetComponent<GameNotifications>();
            var guide = GetComponent<OnboardingGuide>();
            return layout.Overlaps(bounds, guide && guide.IsVisible) || (notifications && notifications.OverlapsWorldLabel(bounds));
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
                DrawStatus(layout.Status); DrawVillage(layout.Village);
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
            Bar(new Rect(r.x + 16, r.y + 66, r.width - 32, 10), DisplayedHealth / maxHP, danger ? Warning : Blood);
            bool enough = !infection || Energy >= infection.InfectionCost;
            Label(r, 16, 83, 120, 22, enough ? "Тёмная энергия" : "Мало энергии", text, enough ? Green : Warning);
            Label(r, 140, 83, r.width - 156, 22, $"{Mathf.FloorToInt(Energy)} / {maxEnergy:0}", right, Ink);
            Bar(new Rect(r.x + 16, r.y + 109, r.width - 32, 10), DisplayedEnergy / maxEnergy, Green);
            var progression = GetComponent<SoulProgression>();
            string progress = progression ? $"Уровень {progression.Level} · Опыт {progression.Experience}/{progression.RequiredExperience}" : "Энергия восстанавливается со временем";
            Label(r, 16, 121, r.width - 32, 20, progress, small, Muted);
            if (progression) Bar(new Rect(r.x + 16, r.y + 140, r.width - 32, 4), (float)progression.Experience / progression.RequiredExperience, Green);
        }

        private void DrawVillage(Rect r)
        {
            Panel(r, Brass);
            Label(r, Inset, 9, r.width - 2 * Inset, 25, "ЖИТЕЛИ ДЕРЕВНИ", title, Ink);
            float column = (r.width - 2 * Inset) / 3;
            DrawVillageCount(r, Inset, column, "Крестьяне", feedback ? feedback.PeasantCount : 0, HUDIcons.Peasant);
            DrawVillageCount(r, Inset + column, column, "Рыцари", feedback ? feedback.KnightCount : 0, HUDIcons.Knight);
            DrawVillageCount(r, Inset + 2 * column, column, "Арбалетчики", feedback ? feedback.RangedCount : 0, HUDIcons.Crossbow);
        }

        private void DrawVillageCount(Rect r, float x, float width, string label, int count, Texture2D icon)
        {
            GUI.color = Brass;
            GUI.DrawTexture(new Rect(r.x + x + (width - 24) / 2, r.y + 42, 24, 24), icon);
            Label(r, x, 70, width, 22, label, columnTitle, Muted);
            Label(r, x, 94, width, 36, count.ToString(), number, Ink);
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
            float width = (r.width - 32) / 3;
            DrawAbility(new Rect(r.x + 8, r.y + 7, width, 66), HUDIcons.Infection,
                "Заражение", "ЛКМ", infection.InfectionCost, Green, "Цель: житель");
            DrawAbility(new Rect(r.x + 16 + width, r.y + 7, width, 66), HUDIcons.Protection,
                "Защита", "ПКМ", protection.EnergyCost, Purple, "Цель: земля");
            DrawAbility(new Rect(r.x + 24 + 2 * width, r.y + 7, width, 66), HUDIcons.Raise,
                "Поднятие", "ЛКМ", infection.RaiseGuardCost, Brass, "Тело / могила");
        }

        private void DrawAbility(Rect tile, Texture2D icon, string name, string input, float cost, Color accent, string target)
        {
            bool aiming = preview && preview.Caption != null && preview.Icon == icon;
            if (aiming) cost = preview.Cost;
            bool funded = manager.Necromancer.Energy.CanSpend(cost);
            bool unavailable = !funded || (aiming && !preview.IsAvailable);
            bool hovered = tile.Contains(Event.current.mousePosition);
            Panel(tile, unavailable ? Warning : hovered || aiming ? accent : Iron);
            GUI.color = unavailable ? Muted : accent;
            GUI.DrawTexture(new Rect(tile.center.x - 10, tile.y + 4, 20, 20), icon);
            Label(tile, 4, 22, tile.width - 8, 16, name, abilityTitle, unavailable ? Muted : Ink);
            Label(tile, 4, 37, tile.width - 8, 14, $"{input} · {cost:0.##} эн.", abilityDetail, Muted);
            string detail = !funded ? "Мало энергии" : aiming && !preview.IsAvailable ? "Цель недоступна"
                : aiming ? "Готово к применению" : hovered ? target : string.Empty;
            Label(tile, 4, 50, tile.width - 8, 14, detail, abilityDetail, unavailable ? Warning : Ink);
        }

        private static void Label(Rect r, float x, float y, float width, float height, string value, GUIStyle style, Color color)
        {
            GUI.color = color; GUI.Label(new Rect(r.x + x, r.y + y, width, height), value, style);
        }
    }
}
