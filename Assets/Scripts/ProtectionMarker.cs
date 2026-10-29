using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    /// <summary>A shield above a unit, using the same query as incoming damage.</summary>
    [DisallowMultipleComponent]
    public sealed class ProtectionMarker : MonoBehaviour
    {
        private UnitCombat combat;
        private LineRenderer shield;
        private Material material;
        public bool IsVisible => shield && shield.enabled;

        private void Awake() => combat = GetComponent<UnitCombat>();

        private void LateUpdate()
        {
            bool protectedNow = combat && ProtectionArea.IncomingDamageMultiplier(combat) < 1;
            if (!protectedNow) { if (shield) shield.enabled = false; return; }
            if (!shield)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = "ProtectionMarker" };
                shield = new GameObject("ProtectedShield").AddComponent<LineRenderer>();
                shield.transform.SetParent(transform, false);
                shield.sharedMaterial = material;
                shield.useWorldSpace = true;
                shield.loop = true;
                shield.positionCount = 5;
                shield.widthMultiplier = 0.045f;
                shield.startColor = shield.endColor = new Color(0.8f, 0.55f, 1, 1);
                shield.shadowCastingMode = ShadowCastingMode.Off;
                shield.receiveShadows = false;
            }
            shield.enabled = true;
            var camera = Camera.main;
            Vector3 right = camera ? camera.transform.right : Vector3.right;
            Vector3 up = camera ? camera.transform.up : Vector3.up;
            Vector3 center = transform.position + Vector3.up * 2.2f;
            shield.SetPosition(0, center + up * 0.16f - right * 0.16f);
            shield.SetPosition(1, center + up * 0.16f + right * 0.16f);
            shield.SetPosition(2, center - up * 0.04f + right * 0.13f);
            shield.SetPosition(3, center - up * 0.2f);
            shield.SetPosition(4, center - up * 0.04f - right * 0.13f);
        }

        private void OnDisable() { if (shield) shield.enabled = false; }
        private void OnDestroy() { if (material) Destroy(material); }
    }
}
