using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Compatibility endpoint for authored scenes; GameHUD draws the unified interface.</summary>
    [RequireComponent(typeof(InfectionAbility))]
    [DisallowMultipleComponent]
    public sealed class DarkEnergyHUD : MonoBehaviour
    {
        public bool ContainsScreenPosition(Vector2 position)
        {
            var hud = FindFirstObjectByType<GameHUD>();
            if (!hud || !hud.IsVisible) return false;
            var layout = hud.Layout;
            Vector2 point = layout.FromScreen(position, Screen.height);
            return layout.Status.Contains(point) || layout.Abilities.Contains(point);
        }
    }
}