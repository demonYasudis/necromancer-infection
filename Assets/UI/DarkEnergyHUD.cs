using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Small, scalable prototype HUD without additional UI package dependencies.</summary>
    [RequireComponent(typeof(InfectionAbility))]
    [DisallowMultipleComponent]
    public sealed class DarkEnergyHUD : MonoBehaviour
    {
        private static readonly Rect Panel = new Rect(18, 18, 340, 172);
        private InfectionAbility ability;
        private GUIStyle titleStyle;
        private GUIStyle hintStyle;
        private GUIStyle valueStyle;
        private float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        private void Awake() => ability = GetComponent<InfectionAbility>();

        public bool ContainsScreenPosition(Vector2 screenPosition) => Scale > 0 && Panel.Contains(
            new Vector2(screenPosition.x, Screen.height - screenPosition.y) / Scale);

        private void OnGUI()
        {
            if (!ability || !ability.Energy || Scale <= 0) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                valueStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleRight };
            }
            Matrix4x4 originalMatrix = GUI.matrix;
            Color originalColor = GUI.color;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
                Fill(Panel, new Color(0.055f, 0.06f, 0.07f, 0.96f));
                Fill(new Rect(18, 18, 4, 172), new Color(0.42f, 0.78f, 0.24f));
                GUI.color = new Color(0.86f, 0.9f, 0.8f);
                GUI.Label(new Rect(34, 28, 210, 28), "ТЁМНАЯ ЭНЕРГИЯ", titleStyle);
                var energy = ability.Energy;
                GUI.Label(new Rect(235, 28, 107, 28),
                    $"{Mathf.FloorToInt(energy.Current)} / {energy.Maximum:0}", valueStyle);
                Fill(new Rect(34, 64, 308, 12), new Color(0.17f, 0.19f, 0.18f));
                bool affordable = energy.CanSpend(ability.InfectionCost);
                Fill(new Rect(34, 64, 308 * energy.Current / energy.Maximum, 12),
                    affordable ? new Color(0.42f, 0.78f, 0.24f) : new Color(0.75f, 0.39f, 0.24f));
                GUI.color = new Color(0.86f, 0.9f, 0.8f);
                GUI.Label(new Rect(34, 82, 308, 24), $"ЛКМ · Заражение: {ability.InfectionCost:0.##}", hintStyle);
                GUI.color = energy.CanSpend(ability.RaiseGuardCost) ? new Color(0.8f, 0.65f, 1) : new Color(1, 0.65f, 0.42f);
                GUI.Label(new Rect(34, 106, 308, 24),
                    $"ЛКМ · Поднять тело стражника: {ability.RaiseGuardCost:0.##}", hintStyle);
                var protection = GetComponent<ProtectionAbility>();
                if (protection && protection.AreaPrefab)
                {
                    bool canProtect = energy.CanSpend(protection.EnergyCost);
                    GUI.color = canProtect ? new Color(0.8f, 0.65f, 1) : new Color(1, 0.65f, 0.42f);
                    GUI.Label(new Rect(34, 130, 308, 24), $"ПКМ · Защита: {protection.EnergyCost:0.##}", hintStyle);
                    GUI.Label(new Rect(34, 154, 308, 24), canProtect
                        ? $"−{protection.AreaPrefab.DamageReduction * 100:0}% урона · {protection.AreaPrefab.Duration:0.#} сек · по земле"
                        : "Для защиты недостаточно энергии", hintStyle);
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
