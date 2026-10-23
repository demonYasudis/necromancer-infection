using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat), typeof(Guard))]
    [DisallowMultipleComponent]
    public sealed class GuardRemains : MonoBehaviour
    {
        [SerializeField] private GuardCorpse corpsePrefab;
        private UnitCombat combat;
        private bool created;

        private void OnEnable()
        {
            combat = GetComponent<UnitCombat>();
            combat.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (combat) combat.Died -= OnDeath;
        }

        private void OnDeath(UnitCombat attacker)
        {
            if (created || !corpsePrefab) return;
            created = true;
            var corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            corpse.name = "GuardCorpse";
        }
    }
}
