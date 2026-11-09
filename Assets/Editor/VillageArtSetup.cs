using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    /// <summary>Bakes reusable village models and dresses the existing playable routes.</summary>
    public static class VillageArtSetup
    {
        private const string Folder = "Assets/Art/Village";
        private const string Models = Folder + "/Models";
        internal const string SceneryName = "Village Scenery";
        internal static readonly string[] Scenes = { DemoLevelSetup.ScenePath, JourneySetup.ScenePath,
            "Assets/Scenes/Tutorial1.unity", "Assets/Scenes/Tutorial2.unity",
            "Assets/Scenes/Tutorial3.unity", "Assets/Scenes/Tutorial4.unity" };
        private static Texture2D atlas;
        private static Mesh cube, cylinder, sphere;
        private static Material roof, wood, stone, wheat, trim, leaf, stalk, iron, amber;

        [MenuItem("Necromancer Infection/Install Village Art")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the scene first.");
            string previous = SceneManager.GetActiveScene().path;
            PrepareAssets();
            CharacterArtSetup.InstallDirections();
            foreach (string path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path);
                DressScene(Object.FindFirstObjectByType<GameManager>());
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Failed to save " + path);
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log("Village materials and models installed on six routes.");
        }

        private static void PrepareAssets()
        {
            if (!AssetDatabase.IsValidFolder(Models)) AssetDatabase.CreateFolder(Folder, "Models");
            string atlasPath = Folder + "/village-surface-atlas.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            roof = Surface("Slate shingles", 2, 1);
            wood = Surface("Weathered planks", 0, 0);
            stone = Surface("Fieldstone", 1, 0);
            wheat = Surface("Cut field", 2, 0, 3);
            trim = Solid("Dark oak", new Color(.20f, .139f, .093f));
            leaf = Solid("Hedgerow", new Color(.215f, .255f, .139f));
            stalk = Solid("Dry straw", new Color(.49f, .37f, .20f));
            iron = Solid("Dull iron", new Color(.26f, .265f, .25f));
            amber = Solid("Lamplight", new Color(.72f, .38f, .14f));
            amber.EnableKeyword("_EMISSION");
            amber.SetColor("_EmissionColor", new Color(.65f, .27f, .055f));
            EditorUtility.SetDirty(amber);

            cube = Primitive(PrimitiveType.Cube);
            cylinder = Primitive(PrimitiveType.Cylinder);
            sphere = Primitive(PrimitiveType.Sphere);
            House("Cottage", 4, 4, 1.9f, .90f, false);
            House("Farmhouse", 6, 4, 2.9f, .95f, true);
            House("Barn", 4.5f, 3.3f, 1.65f, .8f, false);
            BuildProps();
        }

        private static Material Surface(string name, int x, int y, float meters = 0)
        {
            var material = Asset<Material>(Folder + "/" + name + ".mat",
                () => new Material(Shader.Find("Necromancer/Village Surface")));
            material.SetTexture("_MainTex", atlas);
            material.SetVector("_Cell", new Vector4(x, y, 0, 0));
            material.SetFloat("_TileMeters", meters);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Solid(string name, Color color)
        {
            var material = Asset<Material>(Folder + "/" + name + ".mat", () => new Material(Shader.Find("Standard")));
            material.color = color;
            material.SetFloat("_Glossiness", .08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T Asset<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Mesh Primitive(PrimitiveType type)
        {
            var temporary = GameObject.CreatePrimitive(type);
            var mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            return mesh;
        }

        private static void House(string name, float width, float depth, float eave, float rise, bool farm)
        {
            var model = new Model(name);
            model.Box(stone, 0, .18f, 0, width, .36f, depth);
            model.Box(wood, 0, (eave + .32f) / 2, 0, width - .08f, eave - .32f, depth - .08f);
            float pitch = Mathf.Atan2(rise, width * .5f) * Mathf.Rad2Deg;
            float slope = Mathf.Sqrt(rise * rise + width * width / 4) + .32f;
            foreach (int sign in new[] { -1, 1 })
            {
                model.Box(roof, sign * width * .25f, eave + rise * .5f, 0, slope, .16f, depth + .48f,
                    Quaternion.Euler(0, 0, -sign * pitch));
                float z = sign * depth * .5f;
                model.Box(trim, 0, .86f, z, .84f, 1.45f, .10f);
                model.Box(wood, 0, .79f, z + sign * .061f, .62f, 1.30f, .065f);
                model.Box(iron, .19f, .77f, z + sign * .105f, .06f, .09f, .035f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * width * .29f;
                    float windowHeight = farm ? 1.81f : 1.14f;
                    model.Box(trim, x, windowHeight, z, .66f, .72f, .10f);
                    model.Box(amber, x, windowHeight, z + sign * .057f, .44f, .52f, .05f);
                    model.Box(trim, x, windowHeight, z + sign * .091f, .047f, .56f, .04f);
                    model.Box(trim, x, windowHeight, z + sign * .091f, .50f, .045f, .04f);
                    model.Box(trim, side * (width * .5f - .11f), (eave + .3f) / 2, z, .18f, eave - .3f, .15f);
                }
                model.Box(trim, 0, eave - .12f, z, width, .18f, .13f);
                model.Gable(wood, width - .1f, eave, rise - .10f, z, sign);
                float wallX = sign * width * .5f;
                float lightY = farm ? 1.75f : 1.12f;
                foreach (float windowZ in new[] { -.97f, .97f })
                {
                    model.Box(trim, wallX, lightY, windowZ, .13f, .68f, .63f);
                    model.Box(amber, wallX + sign * .066f, lightY, windowZ, .045f, .43f, .45f);
                    model.Box(trim, wallX + sign * .096f, lightY, windowZ, .05f, .47f, .045f);
                }
            }
            model.Box(trim, 0, eave + rise + .055f, 0, .18f, .13f, depth + .52f);
            model.Box(stone, width * .24f, eave + rise * .52f, depth * .22f, .55f, rise, .58f);
            model.Box(stone, width * .24f, eave + rise * 1.03f, depth * .22f, .65f, .15f, .69f);
            model.Save(farm);
        }

        private static void BuildProps()
        {
            var well = new Model("Well");
            well.Part(cylinder, stone, new Vector3(0, .26f, 0), new Vector3(.71f, .26f, .71f));
            well.Part(cylinder, trim, new Vector3(0, .54f, 0), new Vector3(.73f, .025f, .73f));
            well.Part(cylinder, iron, new Vector3(0, .568f, 0), new Vector3(.46f, .01f, .46f));
            well.Box(trim, -.48f, .88f, 0, .12f, 1.76f, .12f);
            well.Box(trim, .48f, .88f, 0, .12f, 1.76f, .12f);
            well.Box(wood, 0, 1.54f, 0, 1.33f, .14f, .15f);
            well.Box(roof, 0, 1.78f, 0, 1.52f, .13f, .84f, Quaternion.Euler(10, 0, 0));
            well.Save();

            var cart = new Model("Cart");
            cart.Box(wood, 0, .44f, 0, 1.42f, .15f, 2.12f);
            foreach (int side in new[] { -1, 1 })
            {
                cart.Box(wood, side * .70f, .70f, 0, .12f, .44f, 2.14f);
                cart.Box(trim, side * .46f, .43f, 1.45f, .12f, .12f, 1.48f);
                cart.Part(cylinder, trim, new Vector3(side * .84f, .41f, -.28f),
                    new Vector3(.40f, .065f, .40f), Quaternion.Euler(0, 0, 90));
                cart.Part(cylinder, iron, new Vector3(side * .91f, .41f, -.28f),
                    new Vector3(.14f, .045f, .14f), Quaternion.Euler(0, 0, 90));
            }
            cart.Box(wood, 0, .65f, -.99f, 1.4f, .44f, .12f);
            cart.Box(stalk, 0, .66f, -.33f, .98f, .35f, .78f);
            cart.Save();

            var supplies = new Model("Supplies");
            for (int i = 0; i < 2; i++)
            {
                float x = -.62f + i * .63f;
                supplies.Part(cylinder, wood, new Vector3(x, .37f, .1f), new Vector3(.29f, .37f, .29f));
                for (int band = 0; band < 2; band++)
                    supplies.Part(cylinder, iron, new Vector3(x, .19f + band * .38f, .1f), new Vector3(.305f, .035f, .305f));
            }
            supplies.Box(wood, .62f, .26f, 0, .54f, .52f, .53f);
            supplies.Box(trim, .62f, .26f, -.278f, .068f, .61f, .045f, Quaternion.Euler(0, 0, 43));
            supplies.Box(wood, .58f, .63f, -.03f, .43f, .23f, .43f);
            supplies.Save();

            var fence = new Model("Fence");
            foreach (float x in new[] { -1.12f, 0, 1.12f })
                fence.Box(wood, x, .42f, 0, .15f, .84f, .17f);
            fence.Box(trim, 0, .32f, 0, 2.38f, .085f, .115f);
            fence.Box(wood, 0, .61f, 0, 2.38f, .10f, .12f);
            fence.Save();

            var field = new Model("Field");
            field.Box(wheat, 0, .025f, 0, 8.4f, .035f, 5.2f);
            for (int row = 0; row < 6; row++)
                for (int i = 0; i < 11; i++)
                {
                    float x = -3.8f + i * .74f + .1f * Mathf.Sin(i + row * 5);
                    float z = -2.18f + row * .86f;
                    field.Box(stalk, x, .13f, z, .041f, .24f, .04f, Quaternion.Euler(10, 0, (i % 3 - 1) * 16));
                }
            field.Save();

            var farmyard = new Model("Farmyard Floor");
            for (int i = -2; i <= 2; i++)
                farmyard.Box(stone, i * .66f, .023f, -.61f + .045f * Mathf.Sin(i), .62f, .026f, .56f);
            farmyard.Save();

            var grave = new Model("Grave");
            grave.Box(wood, 0, .045f, 0, 1.56f, .085f, 2.0f);
            grave.Box(stone, 0, .11f, .70f, 1.02f, .22f, .49f);
            grave.Box(stone, 0, .43f, .77f, .68f, .80f, .26f, Quaternion.Euler(0, 0, -6));
            grave.Box(stone, -.05f, .83f, .77f, .48f, .24f, .25f, Quaternion.Euler(0, 0, -6));
            grave.Box(iron, -.01f, .55f, .619f, .075f, .37f, .034f);
            grave.Box(iron, -.01f, .60f, .616f, .27f, .064f, .035f);
            grave.Save();

            var hedge = new Model("Hedgerow");
            hedge.Part(sphere, leaf, new Vector3(-.31f, .21f, .03f), new Vector3(.58f, .35f, .48f));
            hedge.Part(sphere, leaf, new Vector3(.13f, .29f, .11f), new Vector3(.65f, .44f, .54f));
            hedge.Part(sphere, stone, new Vector3(.43f, .10f, -.15f), new Vector3(.32f, .17f, .29f));
            hedge.Save();

            var torch = new Model("Torch");
            torch.Box(trim, 0, .50f, 0, .12f, 1, .12f);
            torch.Box(iron, 0, .95f, 0, .20f, .16f, .20f);
            torch.Part(sphere, amber, new Vector3(0, 1.12f, 0), new Vector3(.13f, .25f, .13f));
            torch.Save();
        }

        private static void DressScene(GameManager game)
        {
            var old = GameObject.Find(SceneryName);
            if (old) Object.DestroyImmediate(old);
            var scenery = new GameObject(SceneryName).transform;
            game.LevelCamera.backgroundColor = new Color(.085f, .095f, .082f);
            var journey = GameObject.Find("JourneyEnvironment");
            var route = Route(game);
            DressGround(game, journey);
            if (journey)
            {
                var district = new GameObject("Village fronts").transform;
                district.SetParent(scenery, false);
                foreach (Transform node in journey.transform)
                {
                    var renderer = node.GetComponent<Renderer>();
                    if (node.name.StartsWith("House_", StringComparison.Ordinal))
                    {
                        var oldRoof = journey.transform.Find(node.name.Replace("House_", "Roof_"));
                        if (oldRoof) oldRoof.GetComponent<Renderer>().enabled = false;
                        renderer.enabled = false;
                        // Bring the sample frontage into the camera's field while leaving the road clear.
                        if (node.name == "House_2") node.position = new Vector3(node.position.x, node.position.y, 6.8f);
                        Vector3 position = renderer.bounds.center;
                        position.y = 0;
                        Place("Cottage", district, position, FaceRoute(position, route));
                    }
                    if (node.name.StartsWith("Well_", StringComparison.Ordinal))
                    {
                        renderer.enabled = false;
                        Place("Well", district, new Vector3(node.position.x, 0, node.position.z), Quaternion.identity);
                    }
                }
                var sample = Along(route, 26, 6.7f);
                var cottage = Place("Cottage", district, sample, FaceRoute(sample, route));
                var wall = cottage.gameObject.AddComponent<BoxCollider>();
                wall.center = Vector3.up * 1.08f;
                wall.size = new Vector3(3.9f, 2.16f, 3.9f);
                PlaceAlong("Cart", district, route, 20.4f, 4.95f);
                PlaceAlong("Supplies", district, route, 24.2f, 4.82f);
                PlaceAlong("Well", district, route, 30.2f, 5.2f);
                PlaceAlong("Torch", district, route, 26.4f, 4.57f);
                PlaceAlong("Fence", district, route, 22.6f, -4.93f);
                PlaceAlong("Fence", district, route, 20.2f, -4.93f);
                PlaceAlong("Field", scenery, route, 8.7f, 7.7f);
                PlaceAlong("Field", scenery, route, 91, -8.4f);
                PlaceAlong("Field", scenery, route, 153, 8.2f);
                PlaceAlong("Barn", scenery, route, 174, 8.3f);
                PlaceAlong("Cart", scenery, route, 207, -5.15f);
                for (int i = 4; i < 229; i += 4)
                {
                    float side = i % 8 == 0 ? -1 : 1;
                    Vector3 position = Along(route, i, side * (4.6f + .5f * Mathf.Abs(Mathf.Sin(i))));
                    if (ClosestRoutePoint(position, route, out _).sqrMagnitude > 4.0f * 4.0f)
                        Place("Hedgerow", scenery, position, Quaternion.Euler(0, i * 37, 0));
                }
            }
            else
            {
                foreach (string name in new[] { "VillageHouse_A", "VillageHouse_B" })
                {
                    var house = GameObject.Find(name);
                    if (house) ReplaceHouse(house.transform, scenery, "Cottage", route);
                }
                Place("Cart", scenery, new Vector3(-6.4f, 0, 3.8f), Quaternion.Euler(0, 20, 0));
                Place("Fence", scenery, new Vector3(-9, 0, 4.35f), Quaternion.identity);
                Place("Well", scenery, new Vector3(-3.8f, 0, -4.9f), Quaternion.identity);
                Place("Hedgerow", scenery, new Vector3(-11, 0, 4.85f), Quaternion.identity);
            }

            var farm = game.Farm.transform;
            HideDebugMarker(game.StartPoint);
            HideDebugMarker(farm);
            ReplaceHouse(farm.Find("Farmhouse"), scenery, "Farmhouse", route);
            foreach (var renderer in farm.Find("Farmyard").GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = renderer.name == "Trough" ? stone : wood;
            if (farm.GetComponent<FarmAssault>().ClosedGate)
                foreach (var renderer in farm.GetComponent<FarmAssault>().ClosedGate.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.sharedMaterial = wood;
            var yard = farm.position;
            Place("Farmyard Floor", scenery, yard, Quaternion.identity);
            Place("Supplies", scenery, yard + new Vector3(-3.0f, 0, 2.9f), Quaternion.Euler(0, 32, 0));
            Place("Torch", scenery, yard + new Vector3(3.95f, 0, -.75f), Quaternion.identity);
            Place("Torch", scenery, yard + new Vector3(-3.95f, 0, -.75f), Quaternion.identity);
            float fieldSide = journey ? 1 : -1;
            Place("Barn", scenery, yard + new Vector3(fieldSide * 8.2f, 0, 3.3f), Quaternion.identity);
            Place("Field", scenery, yard + new Vector3(fieldSide * 8.1f, 0, 7.8f), Quaternion.identity);
            var grave = Object.FindFirstObjectByType<Grave>();
            if (grave)
            {
                foreach (var renderer in grave.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
                Place("Grave", scenery, grave.transform.position, FaceRoute(grave.transform.position, route));
            }

            // All four tutorials use the Journey layout. Keep one editable set of scenery placements.
            string layoutPath = Models + (journey ? "/Journey Scenery.prefab" : "/Demo Scenery.prefab");
            if (!journey || game.gameObject.scene.path == JourneySetup.ScenePath)
                PrefabUtility.SaveAsPrefabAsset(scenery.gameObject, layoutPath);
            Object.DestroyImmediate(scenery.gameObject);
            var layout = AssetDatabase.LoadAssetAtPath<GameObject>(layoutPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(layout);
            instance.name = SceneryName;
        }

        private static void HideDebugMarker(Transform marker)
        {
            var visual = marker.Find("Visual");
            if (visual && visual.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        }

        private static void DressGround(GameManager game, GameObject journey)
        {
            var footprints = new List<Vector4>();
            if (journey)
            {
                for (int i = 1; i <= 5; i++) AddRoad(journey.transform.Find("Road_" + i).GetComponent<Renderer>(), footprints);
            }
            else
            {
                foreach (string name in new[] { "DirtRoad", "FarmApproach" })
                {
                    var road = GameObject.Find(name);
                    if (road) AddRoad(road.GetComponent<Renderer>(), footprints);
                }
            }
            Vector3 yard = game.Farm.transform.position;
            footprints.Add(new Vector4(yard.x, yard.z + .30f, 3.8f, 3.6f));
            footprints.Add(new Vector4(yard.x, yard.z - 6.2f, 2.15f, 3.6f));
            var ground = Surface("Terrain " + game.gameObject.scene.name, 0, 1, 6);
            ground.EnableKeyword("VILLAGE_TERRAIN");
            for (int i = 0; i < 7; i++)
                ground.SetVector("_Road" + i, i < footprints.Count ? footprints[i] : new Vector4(10000, 10000, 0, 0));
            game.Ground.GetComponent<Renderer>().sharedMaterial = ground;
        }

        private static void AddRoad(Renderer oldRoad, List<Vector4> footprints)
        {
            Bounds shape = oldRoad.bounds;
            footprints.Add(new Vector4(shape.center.x, shape.center.z, shape.extents.x, shape.extents.z));
            oldRoad.enabled = false;
        }

        private static void ReplaceHouse(Transform old, Transform scenery, string model, Vector3[] route)
        {
            if (!old || !old.Find("Walls")) return;
            var walls = old.Find("Walls").GetComponent<Renderer>();
            Vector3 position = walls.bounds.center;
            position.y = walls.bounds.min.y;
            foreach (var renderer in old.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
            Place(model, scenery, position, model == "Farmhouse" ? Quaternion.identity : FaceRoute(position, route));
        }

        private static Vector3[] Route(GameManager game)
        {
            var waypoints = new SerializedObject(game).FindProperty("intermediateWaypoints");
            var points = new Vector3[waypoints.arraySize + 2];
            points[0] = game.StartPoint.position;
            for (int i = 0; i < waypoints.arraySize; i++)
                points[i + 1] = ((Transform)waypoints.GetArrayElementAtIndex(i).objectReferenceValue).position;
            points[points.Length - 1] = game.Farm.ArrivalPoint.position;
            return points;
        }

        private static Vector3 Along(Vector3[] route, float distance, float lateral)
        {
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 delta = route[i] - route[i - 1];
                float length = delta.magnitude;
                if (distance <= length || i == route.Length - 1)
                    return route[i - 1] + delta.normalized * Mathf.Min(length, distance)
                        + Vector3.Cross(Vector3.up, delta.normalized) * lateral;
                distance -= length;
            }
            return route[0];
        }

        // Returns the horizontal offset from the object to the closest point of the route.
        private static Vector3 ClosestRoutePoint(Vector3 position, Vector3[] route, out Vector3 direction)
        {
            Vector3 best = Vector3.one * 10000;
            direction = Vector3.forward;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 delta = route[i] - route[i - 1];
                if (delta.sqrMagnitude < .0001f) continue;
                float t = Mathf.Clamp01(Vector3.Dot(position - route[i - 1], delta) / delta.sqrMagnitude);
                Vector3 offset = route[i - 1] + delta * t - position;
                offset.y = 0;
                if (offset.sqrMagnitude >= best.sqrMagnitude) continue;
                best = offset;
                direction = delta.normalized;
            }
            return best;
        }

        private static Quaternion FaceRoute(Vector3 position, Vector3[] route)
        {
            Vector3 offset = ClosestRoutePoint(position, route, out _);
            return offset.sqrMagnitude < .01f ? Quaternion.identity : Quaternion.LookRotation(offset);
        }

        private static void PlaceAlong(string model, Transform parent, Vector3[] route, float distance, float lateral)
        {
            Vector3 position = Along(route, distance, lateral);
            ClosestRoutePoint(position, route, out var direction);
            // Fences and fields are authored lengthwise on X.
            var rotation = Quaternion.FromToRotation(model == "Fence" || model == "Field" ? Vector3.right : Vector3.forward, direction);
            Place(model, parent, position, rotation);
        }

        private static Transform Place(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + name + ".prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            if (parent.Find(name) != root.transform) root.name = name + " " + parent.childCount;
            root.transform.SetPositionAndRotation(position, rotation);
            return root.transform;
        }

        private sealed class Model
        {
            private readonly string name;
            private readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();
            private readonly List<Mesh> temporary = new List<Mesh>();

            public Model(string name) => this.name = name;

            public void Box(Material material, float x, float y, float z, float width, float height, float depth,
                Quaternion? rotation = null)
            {
                Part(cube, material, new Vector3(x, y, z), new Vector3(width, height, depth), rotation);
            }

            public void Part(Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
            {
                if (!parts.TryGetValue(material, out var list)) parts.Add(material, list = new List<CombineInstance>());
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation ?? Quaternion.identity, scale) });
            }

            public void Gable(Material material, float width, float height, float rise, float z, int sign)
            {
                var mesh = new Mesh
                {
                    vertices = new[] { new Vector3(-width / 2, height, z), new Vector3(width / 2, height, z),
                        new Vector3(0, height + rise, z) },
                    uv = new[] { Vector2.zero, Vector2.right, new Vector2(.5f, 1) },
                    triangles = sign > 0 ? new[] { 0, 1, 2 } : new[] { 0, 2, 1 }
                };
                mesh.RecalculateNormals();
                temporary.Add(mesh);
                Part(mesh, material, Vector3.zero, Vector3.one);
            }

            public void Save(bool farm = false)
            {
                var root = new GameObject(name);
                string path = Models + "/" + name + " meshes.asset";
                Renderer windows = null;
                foreach (var entry in parts)
                {
                    string label = entry.Key.name;
                    var combined = new Mesh { name = label };
                    combined.CombineMeshes(entry.Value.ToArray());
                    Mesh saved = Array.Find(AssetDatabase.LoadAllAssetsAtPath(path),
                        asset => asset is Mesh && asset.name == label) as Mesh;
                    if (saved)
                    {
                        EditorUtility.CopySerialized(combined, saved);
                        EditorUtility.SetDirty(saved);
                        Object.DestroyImmediate(combined);
                    }
                    else
                    {
                        saved = combined;
                        if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.AddObjectToAsset(saved, path);
                        else AssetDatabase.CreateAsset(saved, path);
                    }
                    var part = new GameObject(label);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = saved;
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = entry.Key;
                    if (entry.Key == amber) windows = renderer;
                }
                if (farm)
                {
                    var lights = new SerializedObject(root.AddComponent<VillageFarmLights>());
                    lights.FindProperty("windows").objectReferenceValue = windows;
                    lights.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, Models + "/" + name + ".prefab");
                Object.DestroyImmediate(root);
                foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
            }
        }
    }
}
