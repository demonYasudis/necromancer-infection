using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>A non-blocking, single-use target for paid resurrection.</summary>
    [DisallowMultipleComponent]
    public sealed class GuardCorpse : MonoBehaviour
    {
        [SerializeField] private Zombie raisedGuardPrefab;
        public bool IsConsumed { get; private set; }

        public bool TryRaise(Necromancer leader, float cost)
        {
            if (!isActiveAndEnabled || IsConsumed || Time.timeScale <= 0 || !raisedGuardPrefab
                || !leader || !leader.gameObject.activeInHierarchy || !leader.Combat.IsAlive
                || leader.gameObject.scene != gameObject.scene || !leader.Energy.CanSpend(cost)) return false;
            IsConsumed = true;
            if (!leader.Energy.TrySpend(cost))
            {
                IsConsumed = false;
                return false;
            }
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            var undead = Instantiate(raisedGuardPrefab, transform.position, transform.rotation);
            undead.name = "RaisedGuard";
            undead.Initialize(leader);
            Destroy(gameObject);
            return true;
        }
    }
}
