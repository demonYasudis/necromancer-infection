using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>A close travelling view, shared by the short and extended routes.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class JourneyCamera : MonoBehaviour
    {
        private GameManager manager;
        private LevelObjective objective;
        private FarmAssault assault;
        private Transform rig;
        private readonly List<Vector3> finalApproaches = new List<Vector3>();
        private Vector3 finalDirection;
        private Vector3 cameraRestOffset;
        private float finalBlend;

        private void Start()
        {
            manager = GetComponent<GameManager>();
            if (!manager || !manager.LevelCamera || !manager.Necromancer) return;
            objective = manager.Objective;
            assault = manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            var camera = manager.LevelCamera;
            rig = camera.transform.parent;
            if (!rig)
            {
                rig = new GameObject("CloseCameraRig").transform;
                rig.SetParent(transform, false);
                camera.transform.SetParent(rig, true);
            }
            // The rig owns heading; the camera retains its pitch and local feedback shake.
            rig.rotation = Quaternion.LookRotation(manager.Necromancer.TravelDirection, Vector3.up);
            cameraRestOffset = new Vector3(0, 22, -22);
            camera.transform.localPosition = cameraRestOffset;
            camera.transform.localRotation = Quaternion.Euler(45, 0, 0);
            camera.orthographic = true;
            FrameLeader();
        }

        private void LateUpdate()
        {
            if (!rig || !manager || !manager.Necromancer || manager.IsFinished
                || manager.IsRestarting || Time.timeScale <= 0) return;

            if (manager.FinalPhase && finalApproaches.Count == 0) CaptureFinalApproach();
            bool inEncounter = finalApproaches.Count > 0;
            Vector3 heading = inEncounter ? finalDirection : manager.Necromancer.TravelDirection;
            float turnSpeed = inEncounter ? 180 : 360;
            rig.rotation = Quaternion.RotateTowards(rig.rotation,
                Quaternion.LookRotation(heading, Vector3.up), turnSpeed * Time.deltaTime);
            if (inEncounter) finalBlend = Mathf.MoveTowards(finalBlend, 1, 2 * Time.deltaTime);
            FrameLeader();
        }

        private void CaptureFinalApproach()
        {
            if (objective)
            {
                if (!objective.EncounterStarted || !objective.Encounter) return;
                foreach (Transform defender in objective.Encounter)
                    finalApproaches.Add(defender.position);
            }
            else if (assault)
            {
                assault.AddApproachPositions(finalApproaches);
            }
            if (finalApproaches.Count == 0) return;

            // Hold the direction of the encounter as units move and successive waves arrive.
            Vector3 center = Vector3.zero;
            foreach (Vector3 position in finalApproaches) center += position;
            center /= finalApproaches.Count;
            // Include the yard in the tutorial heading so a nearby defender is not lined up behind the leader.
            if (objective) center = (center + manager.Farm.transform.position) * .5f;
            finalDirection = Vector3.ProjectOnPlane(center - manager.Farm.ArrivalPoint.position, Vector3.up);
            if (finalDirection.sqrMagnitude < .0001f) finalDirection = rig.forward;
            manager.Necromancer.Horde.FaceEncounter(finalDirection);
        }

        private void FrameLeader()
        {
            var camera = manager.LevelCamera;
            if (!objective && finalBlend > 0)
            {
                // Look over the farmhouse roof when viewing the road from the far side of the yard.
                Quaternion pitch = Quaternion.Euler(Mathf.Lerp(45, 60, finalBlend), 0, 0);
                Vector3 offset = pitch * Vector3.back * (22 * Mathf.Sqrt(2));
                camera.transform.localPosition += offset - cameraRestOffset;
                cameraRestOffset = offset;
                camera.transform.localRotation = pitch;
            }
            var layout = new HUDLayout(camera.pixelWidth, camera.pixelHeight);
            // Leave room below the leader for the farm when the encounter faces back down the road.
            float leaderPlacement = Mathf.Lerp(.82f, objective ? .72f : .66f, finalBlend);
            float targetFromTop = Mathf.Lerp(layout.PlayTop, layout.PlayBottom, leaderPlacement);
            float targetY = 1 - targetFromTop * layout.Scale / Mathf.Max(1, camera.pixelHeight);
            float aspect = Mathf.Max(.5f, camera.aspect);
            // Cap the travelling view horizontally so ultrawide screens do not reveal the whole road.
            float zoom = Mathf.Min(5, 8.89f / aspect);
            Vector3 leaderPosition = manager.Necromancer.transform.position;
            if (finalApproaches.Count > 0)
            {
                float pixelsToViewport = layout.Scale / Mathf.Max(1, camera.pixelHeight);
                float above = (targetFromTop - layout.PlayTop) * pixelsToViewport;
                float below = (layout.PlayBottom - targetFromTop) * pixelsToViewport;
                Vector3 torso = leaderPosition + Vector3.up;
                foreach (Vector3 position in finalApproaches)
                    zoom = Mathf.Max(zoom, FitPoint(position + Vector3.up - torso, above, below, aspect));
                zoom = Mathf.Max(zoom, FitPoint(manager.Farm.transform.position + Vector3.up * .5f - torso,
                    above, below, aspect));
                zoom = Mathf.MoveTowards(camera.orthographicSize, zoom, 10 * Time.deltaTime);
            }
            camera.orthographicSize = zoom;
            float screenOffset = (.5f - targetY) * 2 * camera.orthographicSize;
            float torsoOffset = Vector3.Dot(Vector3.up, camera.transform.up);
            Vector3 forward = rig.forward;
            float lookAhead = (screenOffset + torsoOffset) / Vector3.Dot(forward, camera.transform.up);
            // Move the rig, preserving the camera's local hit/win shake.
            rig.position = leaderPosition + forward * lookAhead;
        }

        private float FitPoint(Vector3 offset, float above, float below, float aspect)
        {
            const float padding = .6f;
            float horizontal = (Mathf.Abs(Vector3.Dot(offset, rig.right)) + padding) / aspect;
            float vertical = Vector3.Dot(offset, manager.LevelCamera.transform.up);
            return Mathf.Max(horizontal, Mathf.Max((vertical + padding) / (2 * above),
                (padding - vertical) / (2 * below)));
        }
    }
}
