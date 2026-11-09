using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Connects the peasant sprite to existing civilian gameplay without moving its root or collider.</summary>
    [RequireComponent(typeof(PeasantSpriteRig))]
    public sealed class CivilianSpriteVisual : MonoBehaviour
    {
        private Civilian civilian;
        private UnitCombat combat;
        private CombatMotion motion;
        private PeasantSpriteRig rig;
        private Vector3 previous;
        private Vector2 recoil;
        private float hitAt = -10;
        private float strideVelocity;

        private void Awake()
        {
            civilian = GetComponentInParent<Civilian>();
            combat = GetComponentInParent<UnitCombat>();
            rig = GetComponent<PeasantSpriteRig>();
            rig.PreviewWalking = false;
        }

        private void OnEnable()
        {
            previous = transform.position;
            if (combat)
            {
                combat.HitReceived += OnHit;
                combat.Died += OnDeath;
            }
        }

        private void OnDeath(UnitCombat attacker)
        {
            enabled = false;
            rig.Paused = true;
            SpriteFall.Begin(transform);
        }

        private void OnHit(Vector3 direction)
        {
            var camera = Camera.main;
            recoil = camera ? new Vector2(Vector3.Dot(direction, camera.transform.right), Vector3.Dot(direction, camera.transform.up)).normalized : Vector2.right;
            hitAt = Time.time;
        }

        private void Update()
        {
            Vector3 travel = transform.position - previous;
            previous = transform.position;
            if (!civilian || !combat || Time.deltaTime <= 0) return;
            if (!motion) motion = combat.GetComponent<CombatMotion>();
            var camera = Camera.main;
            float horizontal = camera ? Vector3.Dot(travel, camera.transform.right) : travel.x;
            if (travel.sqrMagnitude < 1 && Mathf.Abs(horizontal) > .0005f) rig.MirrorX = horizontal < 0;
            bool running = civilian.State == Civilian.BehaviourState.Fleeing || civilian.State == Civilian.BehaviourState.RunningToBell;
            rig.Stride = Mathf.SmoothDamp(rig.Stride, running ? 1.35f : .85f, ref strideVelocity, .16f, Mathf.Infinity, Time.deltaTime);
            rig.InfectionBend = motion && motion.CurrentTransformation == CombatMotion.Transformation.Infection
                ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(motion.TransformationProgress / .7f)) : 0;
            float hit = Mathf.Clamp01((Time.time - hitAt) / .16f);
            Vector2 offset = recoil * (.06f * Mathf.Sin(hit * Mathf.PI));
            if (rig.MirrorX) offset.x = -offset.x;
            rig.RecoilOffset = offset;
        }

        private void OnDisable()
        {
            if (combat)
            {
                combat.HitReceived -= OnHit;
                combat.Died -= OnDeath;
            }
            if (!rig) return;
            rig.InfectionBend = 0;
            rig.RecoilOffset = Vector2.zero;
            hitAt = -10;
            rig.SetIdlePose();
        }
    }
}
