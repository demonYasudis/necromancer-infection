using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Shared screen and HUD rejection for ability queries and clicks.</summary>
    public static class AbilityPointer
    {
        public static bool IsBlocked(Vector2 position)
        {
            if (position.x < 0 || position.y < 0 || position.x >= Screen.width || position.y >= Screen.height) return true;
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager && (manager.IsFinished || manager.IsRestarting)) return true;
            var hud = manager ? manager.GetComponent<GameHUD>() : null;
            if (hud && hud.ContainsScreenPosition(position)) return true;
            var energy = Object.FindFirstObjectByType<DarkEnergyHUD>();
            var feedback = Object.FindFirstObjectByType<GameFeedback>();
            var objective = Object.FindFirstObjectByType<FarmObjectiveHUD>();
            return (energy && energy.isActiveAndEnabled && energy.ContainsScreenPosition(position))
                || (feedback && feedback.isActiveAndEnabled && feedback.ContainsScreenPosition(position))
                || (objective && objective.isActiveAndEnabled && objective.ContainsScreenPosition(position));
        }
    }
}
