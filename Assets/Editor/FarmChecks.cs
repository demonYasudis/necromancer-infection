using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class FarmChecks
    {
        public static IEnumerator Checks(GameManager manager)
        {
            CrossbowChecks.DisableSceneArchers();
            var farm = manager.Farm;
            // Isolate traversal from the final battle, which has its own integration scenarios.
            farm.GetComponent<FarmAssault>().enabled = false;
            var leader = manager.Necromancer;
            Require(farm && !manager.FinalPhase && !farm.FinalPhase && farm.transform.Find("Farmhouse")
                && farm.transform.Find("Farmyard") && manager.GetComponent<FarmObjectiveHUD>(),
                "Fresh scene must have a separate farm objective, farmhouse, yard and objective HUD with FinalPhase=false.");
            Require(!farm.TryBeginFinalPhase(leader), "The distant starting necromancer must not start the final phase.");
            Guard patrol = null;
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None))
                if (new SerializedObject(guard).FindProperty("patrolOffsets").arraySize > 0) patrol = guard;
            Require(patrol, "The village road must include a configured patrol.");
            Vector3 patrolStart = patrol.transform.position;
            yield return 0.35f;
            Require(patrol.State == Guard.BehaviourState.Patrolling && Vector3.Distance(patrolStart, patrol.transform.position) > 0.1f,
                "The road guard must actually move between patrol points before detecting undead.");
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            var settings = new SerializedObject(manager);
            var serializedRoute = settings.FindProperty("intermediateWaypoints");
            Require(serializedRoute.arraySize == 4, "Route must explicitly pass village road, village square, patrol approach and road corner.");
            var route = new Transform[serializedRoute.arraySize + 1];
            for (int i = 0; i < serializedRoute.arraySize; i++)
                route[i] = (Transform)serializedRoute.GetArrayElementAtIndex(i).objectReferenceValue;
            route[route.Length - 1] = farm.ArrivalPoint;
            Vector3 previous = manager.StartPoint.position;
            Physics.SyncTransforms();
            foreach (var point in route)
            {
                Vector3 offset = point.position - previous;
                foreach (var hit in Physics.SphereCastAll(previous + Vector3.up * 0.8f, 0.35f,
                    offset.normalized, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    Require(hit.collider.GetComponentInParent<Necromancer>(), "Authored route must not pass through a building or farm fence.");
                previous = point.position;
            }
            int phaseEvents = 0;
            farm.FinalPhaseStarted += () => phaseEvents++;
            int visited = 0;
            double deadline = EditorApplication.timeSinceStartup + 18;
            while (!leader.HasArrived && EditorApplication.timeSinceStartup < deadline)
            {
                if (visited < route.Length && Vector3.Distance(leader.transform.position, route[visited].position) < 0.6f) visited++;
                Vector3 offset = leader.transform.position - farm.transform.position;
                offset.y = 0;
                if (manager.FinalPhase) Require(offset.magnitude <= farm.ActivationRadius + 0.05f && phaseEvents == 1,
                    "FinalPhase must start only inside the farm radius, with a single event.");
                yield return 0.1f;
            }
            if (visited < route.Length && Vector3.Distance(leader.transform.position, route[visited].position) < 0.6f) visited++;
            Require(leader.HasArrived && visited == route.Length && manager.FinalPhase && phaseEvents == 1
                && Vector3.Distance(leader.transform.position, farm.ArrivalPoint.position) < 0.01f,
                "The real autonomous leader must visit every route point, reach the farm and start its final phase.");
            Vector3 arrival = leader.transform.position;
            yield return 0.25f;
            Require(leader.transform.position == arrival && !farm.TryBeginFinalPhase(leader) && phaseEvents == 1,
                "Arrival must stop the leader and repeated proximity checks must not repeat the phase event.");
            Debug.Log("PASS: village patrol moves; route clears buildings/fences; autonomous leader visits all points, arrives and starts FinalPhase once.");

            leader.transform.position = farm.transform.position + Vector3.left * (farm.ActivationRadius + 1);
            Require(manager.FinalPhase && !farm.TryBeginFinalPhase(leader), "Leaving the farm must not reset an already started final phase.");
            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && !farm.TryBeginFinalPhase(leader), "A dead leader cannot start another phase; defeat still works at the farm.");
            Require(manager.TryRestartLevel(), "Retry must be available after death during the final phase.");
            yield return 0.3f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            Require(fresh && fresh.Farm && !fresh.FinalPhase && !fresh.IsDefeated && Time.timeScale == 1,
                "A new attempt must reset FinalPhase and restore the farm route.");
            var freshLeader = fresh.Necromancer;
            fresh.Farm.GetComponent<FarmAssault>().enabled = false;
            freshLeader.enabled = false;
            freshLeader.transform.position = fresh.CaptureTarget.position;
            Require(!fresh.Farm.TryBeginFinalPhase(freshLeader), "A disabled leader must not trigger the final phase.");
            freshLeader.enabled = true;
            Time.timeScale = 0;
            Require(!fresh.Farm.TryBeginFinalPhase(freshLeader) && !fresh.FinalPhase, "Paused gameplay must not trigger the final phase.");
            Time.timeScale = 1;
            fresh.Farm.enabled = false;
            Require(!fresh.Farm.TryBeginFinalPhase(freshLeader), "A disabled objective must not start its phase.");
            fresh.Farm.enabled = true;
            Require(fresh.Farm.TryBeginFinalPhase(freshLeader) && fresh.FinalPhase, "An active living leader at the farm must trigger FinalPhase.");
            Debug.Log("PASS: farm phase stays latched, resets on retry, rejects paused/disabled/dead actors and preserves defeat/retry.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
