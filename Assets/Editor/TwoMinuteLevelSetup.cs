using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class TwoMinuteLevelSetup
    {
        private const float Ratio = 238.5f / 680f;
        private static readonly Vector3[] Old = {
            new Vector3(-189, 0, 0), new Vector3(11, 0, 0), new Vector3(11, 0, 40),
            new Vector3(-189, 0, 40), new Vector3(-189, 0, 80), new Vector3(11, 0, 80)
        };
        private static readonly Vector3[] New = {
            new Vector3(-41.833333f, 0, 0), new Vector3(11, 0, 0), new Vector3(11, 0, 40),
            new Vector3(-41.833333f, 0, 40), new Vector3(-41.833333f, 0, 80), new Vector3(11, 0, 80)
        };

        [MenuItem("Necromancer Infection/Set Journey to Two Minutes %&u")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes first.");
            var scene = EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            var game = Object.FindFirstObjectByType<GameManager>();
            if (Mathf.Abs(game.StartPoint.position.x - New[0].x) < .01f) return;
            if (Mathf.Abs(game.StartPoint.position.x - Old[0].x) > .01f)
                throw new InvalidOperationException("Unexpected route layout; refusing to remap twice.");
            foreach (string name in new[] { "JourneyCivilians", "JourneyPatrols", "JourneyEnvironment" })
                foreach (Transform child in GameObject.Find(name).transform)
                {
                    if (child.name.StartsWith("Road_")) continue;
                    Move(child, Map(child.position));
                }
            Move(Object.FindFirstObjectByType<AlarmRunner>().transform, Map(Object.FindFirstObjectByType<AlarmRunner>().transform.position));
            Move(Object.FindFirstObjectByType<AlarmBell>().transform, Map(Object.FindFirstObjectByType<AlarmBell>().transform.position));
            var graveyard = GameObject.Find("OptionalGraveyard").transform;
            Move(graveyard, Map(graveyard.position));
            Move(game.StartPoint, New[0]);
            Move(game.Necromancer.transform, New[0]);
            var settings = new SerializedObject(game);
            var route = settings.FindProperty("intermediateWaypoints");
            for (int i = 0; i < route.arraySize; i++)
                Move((Transform)route.GetArrayElementAtIndex(i).objectReferenceValue, New[i + 1]);
            for (int i = 1; i < New.Length; i++)
            {
                var road = GameObject.Find("Road_" + i).transform;
                Vector3 delta = New[i] - New[i - 1];
                road.position = (New[i] + New[i - 1]) / 2 + Vector3.up * .015f;
                road.localScale = new Vector3(Mathf.Abs(delta.x) + 5, .02f, Mathf.Abs(delta.z) + 5);
            }
            game.Ground.transform.position = new Vector3(-15.416667f, -.25f, 40);
            game.Ground.transform.localScale = new Vector3(93, .5f, 140);
            Move(GameObject.Find("JourneyCameraRig").transform, New[0]);
            var journey = new SerializedObject(game.GetComponent<JourneyRoute>());
            journey.FindProperty("villageStartsAt").floatValue = 80 * Ratio;
            journey.FindProperty("approachStartsAt").floatValue = 440 * Ratio;
            journey.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            // Keep the route clear when a roadside house lands near a new bend.
            foreach (Transform child in GameObject.Find("JourneyEnvironment").transform)
            {
                var body = child.GetComponent<Collider>();
                if (!body) continue;
                bool blocks = false;
                for (int i = 1; i < New.Length; i++)
                {
                    var delta = New[i] - New[i - 1];
                    foreach (var hit in Physics.SphereCastAll(New[i - 1] + Vector3.up * .8f, .45f, delta.normalized, delta.magnitude))
                        if (hit.collider == body) blocks = true;
                }
                if (!blocks) continue;
                // Move the entire building pair away from the road, retaining its scale.
                Vector3 offset = Vector3.back * 18;
                child.position += offset;
                if (child.name.StartsWith("House_"))
                {
                    var roof = GameObject.Find(child.name.Replace("House_", "Roof_"));
                    if (roof) roof.transform.position += offset;
                }
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Two-minute JourneyLevel saved: 240m at 2m/s; original encounters and three districts retained.");
        }

        private static void Move(Transform target, Vector3 position)
        {
            target.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static Vector3 Map(Vector3 position)
        {
            float best = float.PositiveInfinity, travelled = 0, along = 0;
            Vector3 lateral = Vector3.zero, oldDirection = Vector3.right;
            for (int i = 1; i < Old.Length; i++)
            {
                Vector3 delta = Old[i] - Old[i - 1];
                float t = Mathf.Clamp01(Vector3.Dot(position - Old[i - 1], delta) / delta.sqrMagnitude);
                Vector3 offset = position - (Old[i - 1] + delta * t);
                if (offset.sqrMagnitude < best)
                {
                    best = offset.sqrMagnitude;
                    along = travelled + delta.magnitude * t;
                    lateral = offset;
                    oldDirection = delta.normalized;
                }
                travelled += delta.magnitude;
            }
            along *= Ratio;
            for (int i = 1; i < New.Length; i++)
            {
                Vector3 delta = New[i] - New[i - 1];
                if (along <= delta.magnitude || i == New.Length - 1)
                    return New[i - 1] + delta.normalized * along
                        + Quaternion.FromToRotation(oldDirection, delta.normalized) * lateral;
                along -= delta.magnitude;
            }
            return position;
        }
    }
}
