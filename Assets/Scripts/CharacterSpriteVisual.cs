using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Displays the approved intact artwork while gameplay retains its 3D root and collider.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CharacterSpriteVisual : MonoBehaviour
    {
        public SpriteRenderer Image;
        [SerializeField] private Sprite rear;
        private UnitCombat combat;
        private CombatMotion motion;
        private Crossbowman archer;
        private InfectionAbility infection;
        private ProtectionAbility protection;
        private Sprite front;
        private Vector3 previous;
        private Vector3 restPosition, restScale, facing, recoil;
        private Color palette = Color.white;
        private float tilt, phase;
        private float hitAt = -10;
        private bool fromBehind;
        private int shots;
        private float firedAt = -10;
        private float castAt = -10;

        private void Awake()
        {
            combat = GetComponentInParent<UnitCombat>();
            if (!combat || !Image) return;
            archer = combat.GetComponent<Crossbowman>();
            if (combat.GetComponent<Necromancer>())
            {
                infection = FindFirstObjectByType<InfectionAbility>();
                protection = FindFirstObjectByType<ProtectionAbility>();
            }
            front = Image.sprite;
            restPosition = transform.localPosition;
            restScale = transform.localScale;
            facing = combat.transform.forward;
            phase = (GetInstanceID() & 31) * .21f;
            if (combat.GetComponent<RaisedGuard>()) palette = new Color(.78f, .99f, .71f);
            else if (combat.GetComponent<Zombie>() && !combat.GetComponent<Skeleton>())
                palette = new Color(.82f, .96f, .74f);
        }

        private void OnEnable()
        {
            if (!combat) return;
            previous = combat.transform.position;
            combat.HitReceived += OnHit;
            combat.Died += OnDeath;
            if (infection) infection.InfectedByPlayer += OnCast;
            if (protection) protection.CastByPlayer += OnCast;
        }

        private void OnCast() => castAt = Time.time;

        private void OnHit(Vector3 direction)
        {
            recoil = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            hitAt = Time.time;
        }

        private void OnDeath(UnitCombat attacker)
        {
            if (!Image || !Image.sprite) return;
            enabled = false;
            SpriteFall.Begin(transform);
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (!combat || !Image || !camera) return;
            var view = camera.transform;
            Vector3 travel = combat.transform.position - previous;
            previous = combat.transform.position;
            if (Time.deltaTime > 0) Pose(view, travel);
            // The camera supplies projection and the animation supplies roll.
            transform.rotation = view.rotation * Quaternion.Euler(0, 0, tilt);
        }

        private void Pose(Transform view, Vector3 travel)
        {
            if (!motion) motion = combat.GetComponent<CombatMotion>();
            bool melee = combat.IsMeleeAttacking;
            bool aiming = archer && archer.State == Crossbowman.BehaviourState.Aiming;
            if (melee) facing = combat.MeleeDirection;
            else if (aiming) facing = combat.transform.forward;
            else if (travel.sqrMagnitude > .000001f && travel.sqrMagnitude < .25f)
                facing = Vector3.ProjectOnPlane(travel, Vector3.up).normalized;

            Vector3 cameraForward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            float depth = Vector3.Dot(cameraForward, facing);
            // Hysteresis keeps a diagonal step from swapping the picture every frame.
            fromBehind = rear && depth > (fromBehind ? .18f : .38f);
            Image.sprite = fromBehind ? rear : front;
            float side = Vector3.Dot(facing, view.right);
            if (Mathf.Abs(side) > .12f) Image.flipX = side < 0;

            float walk = motion ? motion.GaitWeight : 0;
            float step = motion ? Mathf.Sin(motion.GaitPhase) : 0;
            float breath = (1 - walk) * Mathf.Sin(Time.time * 2.8f + phase);
            float bob = .012f * breath + .045f * Mathf.Abs(step) * walk;
            float roll = step * walk * 4;
            float cast = Mathf.Sin(Mathf.Clamp01((Time.time - castAt) / .4f) * Mathf.PI);
            bob += cast * .09f;
            roll += cast * (Image.flipX ? 10 : -10);
            float strike = 0;
            if (melee)
            {
                float elapsed = combat.MeleeElapsed;
                strike = elapsed < combat.MeleeWindup
                    ? -Mathf.Sin(elapsed / combat.MeleeWindup * Mathf.PI / 2) * .5f
                    : 1 - Mathf.Clamp01((elapsed - combat.MeleeWindup) / (combat.MeleeDuration - combat.MeleeWindup));
            }
            if (archer && archer.ShotsFired != shots)
            {
                shots = archer.ShotsFired;
                firedAt = Time.time;
            }
            if (aiming) strike = .18f + .22f * (1 - archer.AimRemaining / archer.AimDuration);
            if (archer) strike -= Mathf.Max(0, 1 - (Time.time - firedAt) / .13f) * .65f;
            roll -= strike * (Image.flipX ? -14 : 14);

            float hit = Mathf.Clamp01((Time.time - hitAt) / .16f);
            Vector3 kick = recoil * (.095f * Mathf.Sin(hit * Mathf.PI));
            float fold = 0;
            bool infected = motion && motion.CurrentTransformation == CombatMotion.Transformation.Infection;
            if (motion && motion.CurrentTransformation != CombatMotion.Transformation.None)
            {
                float progress = Mathf.SmoothStep(0, 1, motion.TransformationProgress);
                fold = infected ? progress : 1 - progress;
                roll += fold * (Image.flipX ? -28 : 28);
            }
            transform.position = combat.transform.TransformPoint(restPosition) + Vector3.up * bob + facing * (.16f * strike) + kick;
            transform.localScale = new Vector3(restScale.x, restScale.y * (1 + .012f * breath - .44f * fold), restScale.z);
            tilt = roll + Vector3.Dot(kick, view.right) * 65;
            Color healthy = infected ? Color.Lerp(palette, new Color(.52f, 1, .31f), .3f + fold * .6f) : palette;
            Image.color = Color.Lerp(healthy * new Color(1, .55f, .55f), healthy, hit);
        }

        private void OnDisable()
        {
            if (combat)
            {
                combat.HitReceived -= OnHit;
                combat.Died -= OnDeath;
            }
            hitAt = -10;
            castAt = -10;
            if (infection) infection.InfectedByPlayer -= OnCast;
            if (protection) protection.CastByPlayer -= OnCast;
            if (Image) Image.color = palette;
        }
    }
}
