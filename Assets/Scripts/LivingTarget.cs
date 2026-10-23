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

        public static UnitCombat FindNearest(Vector3 position, float radius, Vector3 hordeCenter, float hordeRadius)
        {
            UnitCombat nearest = null;
            float bestDistance = radius * radius;
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
                if (offset.sqrMagnitude > bestDistance) continue;
                bestDistance = offset.sqrMagnitude;
                nearest = health;
            }
            return nearest;
        }
    }
}
