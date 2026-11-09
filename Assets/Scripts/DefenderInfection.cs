using System.Collections;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Paid conversion for living defenders, including dynamically spawned reinforcements.</summary>
    [DisallowMultipleComponent]
    public sealed class DefenderInfection : MonoBehaviour
    {
        public bool IsInfected { get; private set; }
        public int AcceptedClicks { get; private set; }
        public int RequiredClicks => GetComponent<Crossbowman>() ? 2 : 3;
        public bool IsFinalClick => AcceptedClicks >= RequiredClicks - 1;
        private UnitCombat combat;
        private Behaviour defender;
        private MagicMotes effect;
        public bool IsAvailableTarget => isActiveAndEnabled && combat && combat.IsAlive
            && defender && defender.isActiveAndEnabled && !IsInfected;

        private void Awake()
        {
            combat = GetComponent<UnitCombat>();
            defender = (Behaviour)GetComponent<Guard>() ?? GetComponent<Crossbowman>();
        }

        public bool TryClick(Zombie prefab, Necromancer leader, float delay)
        {
            if (Time.timeScale <= 0 || !IsAvailableTarget || !prefab || !leader || !leader.Combat.IsAlive || delay <= 0)
                return false;
            if (IsFinalClick && !TryInfect(prefab, leader, delay)) return false;
            AcceptedClicks++;
            return true;
        }

        public bool TryInfect(Zombie prefab, Necromancer leader, float delay)
        {
            if (!IsAvailableTarget || !prefab || !leader || !leader.Combat.IsAlive || delay <= 0) return false;
            IsInfected = true;
            defender.enabled = false; // Cancels the archer's aim as well as further AI attacks.
            combat.CancelAttack();
            GetComponent<CombatMotion>().BeginInfection(delay);
            effect = MagicMotes.Infection(transform);
            StartCoroutine(Convert(prefab, leader, delay));
            return true;
        }

        private IEnumerator Convert(Zombie prefab, Necromancer leader, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!combat.IsAlive) yield break;
            if (!leader || !leader.Combat.IsAlive || !prefab) { Restore(); yield break; }
            var zombie = Instantiate(prefab, transform.position, transform.rotation);
            zombie.name = "Zombie";
            zombie.GetComponent<CombatMotion>().BeginRising();
            zombie.Initialize(leader);
            // Conversion is not a kill: no corpse or duplicate kill reward.
            Destroy(gameObject);
        }

        private void OnDisable() { StopAllCoroutines(); Restore(); AcceptedClicks = 0; }

        private void Restore()
        {
            if (!IsInfected) return;
            IsInfected = false;
            AcceptedClicks = 0;
            if (effect) Destroy(effect.gameObject);
            if (TryGetComponent<CombatMotion>(out var motion)) motion.CancelTransformation();
            if (defender && combat && combat.IsAlive) defender.enabled = true;
        }
    }
}
