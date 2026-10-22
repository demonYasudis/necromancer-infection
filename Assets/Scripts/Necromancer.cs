using System;
using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat))]
    [RequireComponent(typeof(DarkEnergy))]
    [DisallowMultipleComponent]
    public sealed class Necromancer : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float moveSpeed = 2;
        private UnitCombat combat;

        private Transform[] route = Array.Empty<Transform>();
        private int nextWaypoint;

        public UnitCombat Combat => combat ? combat : combat = GetComponent<UnitCombat>();
        public DarkEnergy Energy => GetComponent<DarkEnergy>();
        public int MaxHealth => Combat.MaxHealth;
        public float CurrentHealth => Combat.CurrentHealth;
        public float MoveSpeed => moveSpeed;
        public bool HasArrived => route.Length > 0 && nextWaypoint == route.Length;

        public void BeginRoute(Vector3 startPosition, Transform[] waypoints)
        {
            if (waypoints == null || waypoints.Length == 0 || Array.Exists(waypoints, point => point == null))
                throw new ArgumentException("The necromancer requires a non-empty route without missing waypoints.", nameof(waypoints));

            route = (Transform[])waypoints.Clone();
            nextWaypoint = 0;
            transform.position = startPosition;
        }

        private void Update()
        {
            if (!Combat.IsAlive) return;
            float remainingDistance = moveSpeed * Time.deltaTime;
            // Consume the frame's distance across corners, including on slow frames.
            while (nextWaypoint < route.Length)
            {
                if (route[nextWaypoint] == null)
                {
                    Debug.LogError("A necromancer route waypoint was removed during movement.", this);
                    enabled = false;
                    return;
                }

                Vector3 target = route[nextWaypoint].position;
                target.y = transform.position.y;
                Vector3 direction = target - transform.position;
                float distance = direction.magnitude;
                if (distance > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(direction);

                if (distance > remainingDistance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                    return;
                }

                transform.position = target;
                remainingDistance -= distance;
                nextWaypoint++;
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.01f, moveSpeed);
        }
    }
}
