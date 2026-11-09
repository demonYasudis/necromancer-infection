using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat))]
    [DisallowMultipleComponent]
    public sealed class LivingTarget : MonoBehaviour
    {
        private static readonly HashSet<LivingTarget> Active = new HashSet<LivingTarget>();
        private UnitCombat combat;
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public static UnitCombat FindNearest(Vector3 position, float radius, Vector3 hordeCenter, float hordeRadius,
            Vector3 forward = default, UnitCombat preferred = null)
        {
            UnitCombat nearest = null;
            float bestScore = float.PositiveInfinity;
            float detectionSquared = radius * radius;
            float retentionSquared = detectionSquared * 1.44f;
            foreach (var living in Active)
            {
                if (!living) continue;
                var health = living.combat ? living.combat : living.combat = living.GetComponent<UnitCombat>();
                if (!health.isActiveAndEnabled || !health.IsAlive) continue;
                if (living.TryGetComponent<Civilian>(out var human) && !human.IsAvailableTarget) continue;
                if (living.TryGetComponent<Guard>(out var guard) && !guard.isActiveAndEnabled) continue;
                if (living.TryGetComponent<Crossbowman>(out var archer) && !archer.isActiveAndEnabled) continue;
                Vector3 fromHorde = living.transform.position - hordeCenter;
                fromHorde.y = 0;
                if (fromHorde.sqrMagnitude > hordeRadius * hordeRadius) continue;
                Vector3 offset = living.transform.position - position;
                offset.y = 0;
                float distance = offset.sqrMagnitude;
                if (health == preferred && distance <= retentionSquared) return health;
                if (distance > detectionSquared) continue;
                // Prefer enemies in front, but still defend against a nearby flank or rear attack.
                float score = distance + (Vector3.Dot(fromHorde, forward) < -.6f ? 4 : 0);
                if (score >= bestScore) continue;
                bestScore = score;
                nearest = health;
            }
            return nearest;
        }
    }
}
