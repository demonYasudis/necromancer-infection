using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>The leader's resource pool. All regeneration uses scaled game time.</summary>
    [DisallowMultipleComponent]
    public sealed class DarkEnergy : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maxEnergy = 100;
        [SerializeField, Min(0)] private float startingEnergy = 100;
        [SerializeField, Min(0)] private float regenerationPerSecond = 1;
        [SerializeField, Min(0)] private float zombieReward = 5;
        [SerializeField, Min(0)] private float humanDeathReward = 3;
        [SerializeField, Min(0)] private float guardDeathReward = 8;
        [SerializeField] private float currentEnergy;
        private Necromancer owner;

        public float Current => currentEnergy;
        public float Maximum => maxEnergy;
        public bool IsAvailable => isActiveAndEnabled && owner && owner.Combat.IsAlive;

        private void Awake()
        {
            owner = GetComponent<Necromancer>();
            currentEnergy = Mathf.Clamp(startingEnergy, 0, maxEnergy);
        }

        private void OnEnable()
        {
            UnitCombat.AnyDied += OnUnitDied;
            Zombie.Raised += OnZombieRaised;
        }

        private void OnDisable()
        {
            UnitCombat.AnyDied -= OnUnitDied;
            Zombie.Raised -= OnZombieRaised;
        }

        private void Update() => Restore(regenerationPerSecond * Time.deltaTime);

        public bool CanSpend(float amount) => IsAvailable && !float.IsNaN(amount)
            && !float.IsInfinity(amount) && amount >= 0 && currentEnergy >= amount;

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            currentEnergy -= amount;
            return true;
        }

        public void Restore(float amount)
        {
            if (!IsAvailable || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        }

        private void OnUnitDied(UnitCombat victim)
        {
            if (victim.gameObject.scene != gameObject.scene) return;
            if (victim.GetComponent<Civilian>()) Restore(humanDeathReward);
            else if (victim.GetComponent<Guard>()) Restore(guardDeathReward);
        }

        private void OnZombieRaised(Zombie zombie)
        {
            if (zombie.Leader == owner) Restore(zombieReward);
        }

        private void OnValidate()
        {
            maxEnergy = Mathf.Max(1, maxEnergy);
            startingEnergy = Mathf.Clamp(startingEnergy, 0, maxEnergy);
            regenerationPerSecond = Mathf.Max(0, regenerationPerSecond);
            zombieReward = Mathf.Max(0, zombieReward);
            humanDeathReward = Mathf.Max(0, humanDeathReward);
            guardDeathReward = Mathf.Max(0, guardDeathReward);
        }
    }
}
