using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Marks threats that living villagers can detect, including future zombies.</summary>
    [DisallowMultipleComponent]
    public sealed class UndeadPresence : MonoBehaviour
    {
        private static readonly HashSet<UndeadPresence> Active = new HashSet<UndeadPresence>();

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public static Transform FindNearest(Vector3 position, float radius)
        {
            Transform nearest = null;
            float bestDistance = radius * radius;
            foreach (var undead in Active)
            {
                if (undead == null) continue;
                if (undead.TryGetComponent<UnitCombat>(out var combat) && !combat.IsAlive) continue;
                Vector3 offset = undead.transform.position - position;
                offset.y = 0;
                if (offset.sqrMagnitude > bestDistance) continue;
                bestDistance = offset.sqrMagnitude;
                nearest = undead.transform;
            }
            return nearest;
        }

        public static UnitCombat FindCombatTarget(Vector3 position, float radius, float priorityRadius,
            Vector3 home, float chaseRadius)
        {
            UnitCombat nearest = null;
            UnitCombat necromancer = null;
            float bestDistance = radius * radius;
            float priorityDistance = priorityRadius * priorityRadius;
            foreach (var undead in Active)
            {
                if (!undead || !undead.TryGetComponent<UnitCombat>(out var health)
                    || !health.isActiveAndEnabled || !health.IsAlive) continue;
                Vector3 fromHome = undead.transform.position - home;
                fromHome.y = 0;
                if (fromHome.sqrMagnitude > chaseRadius * chaseRadius) continue;
                Vector3 offset = undead.transform.position - position;
                offset.y = 0;
                float distance = offset.sqrMagnitude;
                if (distance <= priorityDistance && undead.GetComponent<Necromancer>())
                {
                    priorityDistance = distance;
                    necromancer = health;
                }
                if (distance > bestDistance) continue;
                bestDistance = distance;
                nearest = health;
            }
            return necromancer ? necromancer : nearest;
        }
    }
}
