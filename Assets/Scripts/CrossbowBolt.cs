using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>A straight, swept projectile; protection is evaluated at impact.</summary>
    public sealed class CrossbowBolt : MonoBehaviour
    {
        private UnitCombat shooter;
        private int damage;
        private Vector3 direction;
        private float speed;
        private float remainingDistance;
        private Material material;
        private LineRenderer visual;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[16];

        public static CrossbowBolt Launch(UnitCombat source, Vector3 origin, Vector3 destination, float speed, float range)
        {
            var bolt = new GameObject("CrossbowBolt").AddComponent<CrossbowBolt>();
            bolt.shooter = source;
            bolt.damage = source.Damage;
            bolt.transform.position = origin;
            bolt.direction = (destination - origin).normalized;
            bolt.speed = speed;
            bolt.remainingDistance = range;
            bolt.material = new Material(Shader.Find("Sprites/Default")) { name = "CrossbowBolt" };
            bolt.visual = bolt.gameObject.AddComponent<LineRenderer>();
            bolt.visual.sharedMaterial = bolt.material;
            bolt.visual.positionCount = 2;
            bolt.visual.widthMultiplier = 0.065f;
            bolt.visual.startColor = new Color(1, 0.85f, 0.4f);
            bolt.visual.endColor = new Color(1, 0.55f, 0.1f);
            bolt.visual.shadowCastingMode = ShadowCastingMode.Off;
            bolt.visual.receiveShadows = false;
            bolt.UpdateVisual();
            return bolt;
        }

        private void Update()
        {
            if (Time.timeScale <= 0 || Time.deltaTime <= 0) return;
            // Sphere casts omit bodies already overlapping their origin (e.g. a zombie in melee).
            int overlapping = Physics.OverlapSphereNonAlloc(transform.position, 0.1f, overlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (overlapping == overlaps.Length)
            {
                Destroy(gameObject);
                return;
            }
            for (int i = 0; i < overlapping; i++)
                if (CanHit(overlaps[i]))
                {
                    Impact(overlaps[i]);
                    return;
                }
            float step = Mathf.Min(speed * Time.deltaTime, remainingDistance);
            int count = Physics.SphereCastNonAlloc(transform.position, 0.1f, direction, hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // Never tunnel through geometry if an exceptionally dense crowd fills the buffer.
            if (count == hits.Length)
            {
                Destroy(gameObject);
                return;
            }
            float closest = float.PositiveInfinity;
            Collider obstacle = null;
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].collider;
                if (!CanHit(body)) continue;
                if (hits[i].distance >= closest) continue;
                closest = hits[i].distance;
                obstacle = body;
            }
            if (obstacle)
            {
                Impact(obstacle);
                return;
            }
            transform.position += direction * step;
            remainingDistance -= step;
            UpdateVisual();
            if (remainingDistance <= 0) Destroy(gameObject);
        }

        private bool CanHit(Collider body)
        {
            var unit = body.GetComponentInParent<UnitCombat>();
            return !unit || (unit != shooter && unit.isActiveAndEnabled && unit.IsAlive && unit.GetComponent<UndeadPresence>());
        }

        private void Impact(Collider body)
        {
            var victim = body.GetComponentInParent<UnitCombat>();
            if (victim && victim.GetComponent<UndeadPresence>()) victim.TakeDamage(damage, shooter);
            Destroy(gameObject);
        }

        private void UpdateVisual()
        {
            visual.SetPosition(0, transform.position);
            visual.SetPosition(1, transform.position - direction * 0.55f);
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
        }
    }
}
