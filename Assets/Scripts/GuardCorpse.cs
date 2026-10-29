using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>A non-blocking, single-use target for paid resurrection.</summary>
    [DisallowMultipleComponent]
    public sealed class GuardCorpse : MonoBehaviour
    {
        [SerializeField] private Zombie raisedGuardPrefab;
        public bool IsConsumed { get; private set; }

        public string UnavailableReason(Necromancer leader)
        {
            if (IsConsumed) return "Тело уже поднято";
            if (!isActiveAndEnabled || !raisedGuardPrefab || !leader || !leader.gameObject.activeInHierarchy
                || !leader.Combat.IsAlive || leader.gameObject.scene != gameObject.scene) return "Поднятие недоступно";
            return null;
        }

        public bool TryRaise(Necromancer leader, float cost)
        {
            if (Time.timeScale <= 0 || UnavailableReason(leader) != null || !leader.Energy.CanSpend(cost)) return false;
            IsConsumed = true;
            if (!leader.Energy.TrySpend(cost))
            {
                IsConsumed = false;
                return false;
            }
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            var undead = Instantiate(raisedGuardPrefab, transform.position, transform.rotation);
            undead.name = "RaisedGuard";
            undead.GetComponent<CombatMotion>().BeginRising();
            undead.Initialize(leader);
            Destroy(gameObject);
            return true;
        }
    }
}
