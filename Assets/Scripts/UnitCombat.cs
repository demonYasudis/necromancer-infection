using System;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Shared health, melee range and per-attacker cooldown. AI chooses the target.</summary>
    [DisallowMultipleComponent]
    public sealed class UnitCombat : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 30;
        [SerializeField, Min(0)] private int damage;
        [SerializeField, Min(0.01f)] private float attackRange = 0.9f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1;
        [SerializeField] private float currentHealth;
        [SerializeField, HideInInspector] private int setupVersion;
        private float nextAttackTime;

        public int MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public int Damage => damage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public bool IsAlive => currentHealth > 0;
        public event Action<UnitCombat> Died;
        public static event Action<UnitCombat> AnyDied;

        private void Awake() => currentHealth = maxHealth;

        public bool TryAttack(UnitCombat target)
        {
            if (!isActiveAndEnabled || !IsAlive || damage <= 0 || Time.timeScale <= 0
                || Time.time < nextAttackTime || !target || target == this
                || !target.isActiveAndEnabled || !target.IsAlive) return false;
            if ((target.transform.position - transform.position).sqrMagnitude > attackRange * attackRange)
                return false;

            // Reserve the cooldown before damage callbacks can trigger further gameplay.
            nextAttackTime = Time.time + attackCooldown;
            return target.TakeDamage(damage, this);
        }

        public bool TakeDamage(int amount, UnitCombat attacker = null)
        {
            if (!isActiveAndEnabled || !IsAlive || amount <= 0 || Time.timeScale <= 0) return false;
            float incoming = amount * ProtectionArea.IncomingDamageMultiplier(this);
            if (incoming <= 0) return false;
            currentHealth = Mathf.Max(0, currentHealth - incoming);
            if (IsAlive) return true;

            // HP reaches zero synchronously: later hits in this frame cannot kill twice.
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            try
            {
                AnyDied?.Invoke(this);
                Died?.Invoke(attacker);
            }
            finally
            {
                Destroy(gameObject);
            }
            return true;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            damage = Mathf.Max(0, damage);
            attackRange = Mathf.Max(0.01f, attackRange);
            attackCooldown = Mathf.Max(0.01f, attackCooldown);
        }
    }
}
