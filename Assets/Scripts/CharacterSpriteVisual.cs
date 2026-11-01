using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Displays the approved intact artwork while gameplay retains its 3D root and collider.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CharacterSpriteVisual : MonoBehaviour
    {
        public SpriteRenderer Image;
        private UnitCombat combat;
        private Vector3 previous;
        private float hitAt = -10;

        private void Awake() => combat = GetComponentInParent<UnitCombat>();

        private void OnEnable()
        {
            previous = transform.position;
            if (combat) combat.HitReceived += OnHit;
        }

        private void OnHit(Vector3 direction) => hitAt = Time.time;

        private void LateUpdate()
        {
            var camera = Camera.main;
            Vector3 travel = transform.position - previous;
            previous = transform.position;
            if (!Image || !camera) return;
            // Cancel the navigation root's yaw: the painting already contains its perspective.
            transform.rotation = camera.transform.rotation;
            if (Time.deltaTime <= 0) return;
            Vector3 facing = combat && combat.IsMeleeAttacking ? combat.MeleeDirection : travel;
            float horizontal = Vector3.Dot(facing, camera.transform.right);
            if (facing.sqrMagnitude < 1 && Mathf.Abs(horizontal) > .0005f)
                Image.flipX = horizontal < 0;
            Image.color = Color.Lerp(new Color(1, .55f, .55f), Color.white,
                Mathf.Clamp01((Time.time - hitAt) / .16f));
        }

        private void OnDisable()
        {
            if (combat) combat.HitReceived -= OnHit;
            hitAt = -10;
            if (Image) Image.color = Color.white;
        }
    }
}
