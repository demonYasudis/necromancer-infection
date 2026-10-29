using System;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Shared health, attack range and per-attacker cooldown. AI chooses the target.</summary>
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
        private UnitCombat meleeTarget;
        private float meleeStartedAt;
        private bool impactResolved;
        public float MeleeWindup => Mathf.Min(attackCooldown * 0.35f,
            GetComponent<RaisedGuard>() ? 0.32f : GetComponent<Guard>() ? 0.18f : 0.24f);
        public float MeleeDuration => MeleeWindup + Mathf.Min(0.28f, attackCooldown * 0.45f);
        public bool IsMeleeAttacking { get; private set; }
        public bool IsMeleeContact => IsMeleeAttacking && MeleeElapsed >= MeleeWindup
            && MeleeElapsed < MeleeWindup + Mathf.Min(0.07f, attackCooldown * 0.1f);
        public float MeleeElapsed => IsMeleeAttacking ? Time.time - meleeStartedAt : 0;
        public Vector3 MeleeDirection { get; private set; }
        public event Action<Vector3> HitReceived;

        public int MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public int Damage => damage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public bool IsAlive => currentHealth > 0;
        public event Action<UnitCombat> Died;
        public static event Action<UnitCombat> AnyDied;
        public static event Action<UnitCombat> AnyDamaged;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (!GetComponent<CombatMotion>()) gameObject.AddComponent<CombatMotion>();
            if (!GetComponent<ProtectionMarker>()) gameObject.AddComponent<ProtectionMarker>();
        }

        private void OnDisable() => CancelMelee();

        private void CancelMelee()
        {
            IsMeleeAttacking = false;
            meleeTarget = null;
        }

        private void Update()
        {
            if (!IsMeleeAttacking || Time.deltaTime <= 0) return;
            if (!IsAlive) { CancelMelee(); return; }
            if (meleeTarget)
            {
                Vector3 direction = meleeTarget.transform.position - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.0001f) MeleeDirection = direction.normalized;
            }
            if (!impactResolved && MeleeElapsed >= MeleeWindup)
            {
                impactResolved = true; // Damage callbacks may finish the attempt or destroy either unit.
                if (CanReach(meleeTarget) && !(meleeTarget.TryGetComponent<Civilian>(out var human)
                    && human.State == Civilian.BehaviourState.Infected))
                    meleeTarget.TakeDamage(damage, this);
                meleeTarget = null;
            }
            if (MeleeElapsed >= MeleeDuration) CancelMelee();
        }

        private bool CanReach(UnitCombat target) => target && target != this
            && target.isActiveAndEnabled && target.IsAlive
            && (target.transform.position - transform.position).sqrMagnitude <= attackRange * attackRange;

        public bool TryAttack(UnitCombat target)
        {
            if (!TryBeginAttack(target)) return false;
            meleeTarget = target;
            meleeStartedAt = Time.time;
            impactResolved = false;
            IsMeleeAttacking = true;
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0;
            MeleeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(MeleeDirection);
            return true;
        }

        /// <summary>Reserves range/cooldown for an attack whose damage may arrive later.</summary>
        public bool TryBeginAttack(UnitCombat target)
        {
            if (!isActiveAndEnabled || !IsAlive || damage <= 0 || Time.timeScale <= 0
                || IsMeleeAttacking || Time.time < nextAttackTime || !CanReach(target)) return false;

            // Reserve the cooldown before damage callbacks can trigger further gameplay.
            nextAttackTime = Time.time + attackCooldown;
            return true;
        }

        public bool TakeDamage(int amount, UnitCombat attacker = null)
        {
            if (!isActiveAndEnabled || !IsAlive || amount <= 0 || Time.timeScale <= 0) return false;
            float incoming = amount * ProtectionArea.IncomingDamageMultiplier(this);
            if (incoming <= 0) return false;
            currentHealth = Mathf.Max(0, currentHealth - incoming);
            HitReceived?.Invoke(attacker ? transform.position - attacker.transform.position : -transform.forward);
            AnyDamaged?.Invoke(this);
            if (IsAlive) return true;

            // HP reaches zero synchronously: later hits in this frame cannot kill twice.
            CancelMelee();
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
