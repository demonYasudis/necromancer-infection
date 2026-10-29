using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Compatibility endpoint; objective drawing belongs to GameHUD.</summary>
    [DisallowMultipleComponent]
    public sealed class FarmObjectiveHUD : MonoBehaviour
    {
        public bool ContainsScreenPosition(Vector2 position)
        {
            var hud = GetComponent<GameHUD>();
            return hud && hud.IsVisible && hud.Layout.Objective.Contains(hud.Layout.FromScreen(position, Screen.height));
        }
    }
}