using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>A stationary ranged defender with a cancellable, visible aiming phase.</summary>
    [RequireComponent(typeof(UnitCombat), typeof(LivingTarget))]
    [DisallowMultipleComponent]
    public sealed class Crossbowman : MonoBehaviour
    {
        public enum BehaviourState { Idle, Aiming, Reloading }
        [SerializeField, Min(0.1f)] private float aimDuration = 1.5f;
        [SerializeField, Min(1)] private float boltSpeed = 24;
        [SerializeField] private Transform muzzle;
        private UnitCombat combat;
        private UnitCombat currentTarget;
        private float aimStartedAt;
        private float nextAimAt;
        private LineRenderer aimLine;
        private LineRenderer targetRing;
        private Material warningMaterial;
        private readonly RaycastHit[] sightHits = new RaycastHit[64];

        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();
        public UnitCombat CurrentTarget => currentTarget;
        public BehaviourState State { get; private set; }
        public float AimDuration => aimDuration;
        public float AimRemaining => State == BehaviourState.Aiming ? Mathf.Max(0, aimDuration - (Time.time - aimStartedAt)) : 0;
        public int ShotsFired { get; private set; }
        public Vector3 MuzzlePosition => currentTarget
            && (currentTarget.transform.position - transform.position).sqrMagnitude < 1.69f
                ? transform.position + Vector3.up * 1.1f
                : muzzle ? muzzle.position : transform.position + Vector3.up * 1.1f;

        private void Awake()
        {
            warningMaterial = new Material(Shader.Find("Sprites/Default")) { name = "CrossbowWarning" };
            aimLine = CreateLine("AimWarning", 2);
            targetRing = CreateLine("TargetWarning", 48);
            targetRing.loop = true;
            HideWarning();
        }

        private LineRenderer CreateLine(string name, int count)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.positionCount = count;
            line.sharedMaterial = warningMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void OnDisable() => CancelAim();
        private void OnDestroy()
        {
            if (warningMaterial) Destroy(warningMaterial);
        }

        private void Update()
        {
            if (Time.timeScale <= 0 || Time.deltaTime <= 0) return;
            if (!Combat.IsAlive)
            {
                CancelAim();
                return;
            }
            if (State == BehaviourState.Aiming)
            {
                if (!CanAimAt(currentTarget))
                {
                    CancelAim();
                    return;
                }
                FaceTarget();
                UpdateWarning();
                if (AimRemaining > 0) return;
                // Recheck the common combat cooldown and range at release, too.
                if (Combat.TryBeginAttack(currentTarget))
                {
                    CrossbowBolt.Launch(Combat, MuzzlePosition, currentTarget.transform.position + Vector3.up * 0.8f,
                        boltSpeed, Combat.AttackRange + 1);
                    ShotsFired++;
                    nextAimAt = Time.time + Combat.AttackCooldown;
                }
                currentTarget = null;
                HideWarning();
                State = BehaviourState.Reloading;
                return;
            }
            if (Time.time < nextAimAt)
            {
                State = BehaviourState.Reloading;
                return;
            }
            currentTarget = UndeadPresence.FindCombatTarget(transform.position, Combat.AttackRange, 2,
                transform.position, Combat.AttackRange);
            if (!CanAimAt(currentTarget))
            {
                CancelAim();
                return;
            }
            aimStartedAt = Time.time;
            State = BehaviourState.Aiming;
            FaceTarget();
            UpdateWarning();
        }

        private bool CanAimAt(UnitCombat target)
        {
            if (!target || !target.isActiveAndEnabled || !target.IsAlive
                || target.gameObject.scene != gameObject.scene || !target.GetComponent<UndeadPresence>()
                || (target.transform.position - transform.position).sqrMagnitude > Combat.AttackRange * Combat.AttackRange)
                return false;
            Vector3 ray = target.transform.position + Vector3.up * 0.8f - MuzzlePosition;
            int count = Physics.RaycastNonAlloc(MuzzlePosition, ray.normalized, sightHits, ray.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].collider;
                // Friendly defenders do not prevent firing from behind the front line.
                if (hit.GetComponentInParent<UnitCombat>()) continue;
                return false;
            }
            return true;
        }

        private void FaceTarget()
        {
            Vector3 direction = currentTarget.transform.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void UpdateWarning()
        {
            float progress = 1 - AimRemaining / aimDuration;
            Color color = Color.Lerp(new Color(1, 0.65f, 0.12f), new Color(1, 0.18f, 0.08f), progress);
            aimLine.enabled = targetRing.enabled = true;
            aimLine.startColor = aimLine.endColor = targetRing.startColor = targetRing.endColor = color;
            aimLine.widthMultiplier = 0.045f + progress * 0.055f;
            targetRing.widthMultiplier = 0.07f;
            aimLine.SetPosition(0, MuzzlePosition);
            aimLine.SetPosition(1, currentTarget.transform.position + Vector3.up * 0.8f);
            float radius = Mathf.Lerp(0.9f, 0.5f, progress);
            for (int i = 0; i < targetRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / targetRing.positionCount;
                targetRing.SetPosition(i, currentTarget.transform.position
                    + new Vector3(Mathf.Cos(angle) * radius, 0.06f, Mathf.Sin(angle) * radius));
            }
        }

        private void HideWarning()
        {
            if (aimLine) aimLine.enabled = false;
            if (targetRing) targetRing.enabled = false;
        }

        private void CancelAim()
        {
            currentTarget = null;
            State = BehaviourState.Idle;
            HideWarning();
        }

        private void OnValidate()
        {
            aimDuration = Mathf.Max(0.1f, aimDuration);
            boltSpeed = Mathf.Max(1, boltSpeed);
        }
    }
}
