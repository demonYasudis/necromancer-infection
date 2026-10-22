using System;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>The farm objective. Arrival starts the final phase once per attempt.</summary>
    [DisallowMultipleComponent]
    public sealed class CaptureTarget : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float activationRadius = 3;
        [SerializeField] private bool finalPhase;
        [SerializeField] private Transform arrivalPoint;

        public float ActivationRadius => activationRadius;
        public bool FinalPhase => finalPhase;
        public Transform ArrivalPoint => arrivalPoint ? arrivalPoint : transform;
        public event Action FinalPhaseStarted;

        private void Awake() => finalPhase = false;

        public bool TryBeginFinalPhase(Necromancer leader)
        {
            if (finalPhase || !isActiveAndEnabled || Time.timeScale <= 0 || !leader
                || !leader.isActiveAndEnabled || !leader.Combat.IsAlive) return false;
            Vector3 offset = leader.transform.position - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude > activationRadius * activationRadius) return false;
            finalPhase = true;
            FinalPhaseStarted?.Invoke();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.4f, 1);
            Vector3 previous = transform.position + Vector3.right * activationRadius;
            for (int i = 1; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * activationRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void OnValidate() => activationRadius = Mathf.Max(0.1f, activationRadius);
    }
}
