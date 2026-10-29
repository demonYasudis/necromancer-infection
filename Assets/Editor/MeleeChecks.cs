using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class MeleeChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Melee checks require a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            manager.Necromancer.enabled = false;
            manager.Necromancer.transform.position = new Vector3(-6, 0, 0);
            CrossbowChecks.DisableSceneArchers();
            foreach (var unit in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) unit.gameObject.SetActive(false);
            foreach (var unit in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) unit.gameObject.SetActive(false);
            var guard = Spawn("Guard", Vector3.zero);
            var zombie = Spawn("Zombie", new Vector3(0, 0, 0.8f));
            var heavy = Spawn("RaisedGuard", new Vector3(2, 0, 0));
            var skeleton = Spawn("Skeleton", new Vector3(-2, 0, 0));
            yield return 0.1f;
            Require(guard.MeleeWindup < zombie.MeleeWindup && zombie.MeleeWindup < heavy.MeleeWindup,
                "Guard, clawing undead and heavy need distinct attack timing.");
            Transform sword = guard.transform.Find("Sword");
            Vector3 rest = sword.localPosition;
            Vector3 root = guard.transform.position;
            var audio = manager.GetComponent<GameAudio>();
            int impacts = audio.PlayedCount(GameAudio.Cue.Impact);
            float hp = zombie.CurrentHealth;
            Require(guard.TryAttack(zombie) && !guard.TryAttack(zombie), "A swing reserves cooldown once.");
            yield return guard.MeleeWindup * 0.45f;
            Require(zombie.CurrentHealth == hp && sword.localPosition != rest
                && guard.transform.position == root && audio.PlayedCount(GameAudio.Cue.Impact) == impacts,
                "Windup moves the weapon without early damage, sound or navigation displacement.");
            Time.timeScale = 0;
            Vector3 paused = sword.localPosition;
            float elapsed = guard.MeleeElapsed;
            yield return 0.3f;
            Require(sword.localPosition == paused && guard.MeleeElapsed == elapsed && zombie.CurrentHealth == hp,
                "Pause freezes windup and prevents delayed damage.");
            Time.timeScale = 1;
            yield return guard.MeleeWindup * 0.65f;
            Require(zombie.CurrentHealth == hp - 15 && audio.PlayedCount(GameAudio.Cue.Impact) == impacts + 1
                && zombie.GetComponent<CombatMotion>().ReactionRemaining > 0,
                "Contact deals one hit, plays one sound and starts recoil on the victim.");
            Capture(manager.LevelCamera);
            Time.timeScale = 0;
            float reaction = zombie.GetComponent<CombatMotion>().ReactionRemaining;
            yield return 0.2f;
            Require(zombie.GetComponent<CombatMotion>().ReactionRemaining == reaction, "Hit reaction also freezes on pause.");
            Time.timeScale = 1;
            yield return guard.AttackCooldown + 0.05f;
            Require(!guard.IsMeleeAttacking && sword.localPosition == rest, "Recovery restores the authored weapon pose.");

            hp = zombie.CurrentHealth;
            Require(guard.TryAttack(zombie), "Range-cancellation setup must start.");
            zombie.transform.position = new Vector3(0, 0, 3);
            yield return guard.MeleeDuration + 0.05f;
            Require(zombie.CurrentHealth == hp, "A target escaping during windup must not be hit remotely.");
            yield return guard.AttackCooldown;
            zombie.transform.position = new Vector3(0, 0, 0.8f);
            Require(guard.TryAttack(zombie), "Disabled-attacker setup must start.");
            guard.enabled = false;
            yield return guard.MeleeDuration + 0.05f;
            Require(zombie.CurrentHealth == hp && !guard.IsMeleeAttacking, "Disabling combat cancels pending damage.");
            guard.enabled = true;
            yield return guard.AttackCooldown;
            Require(guard.TryAttack(zombie), "Disabled-target setup must start.");
            zombie.gameObject.SetActive(false);
            yield return guard.MeleeDuration + 0.05f;
            zombie.gameObject.SetActive(true);
            Require(zombie.CurrentHealth == hp, "An inactive target cannot receive a pending hit.");
            yield return guard.AttackCooldown;
            Require(guard.TryAttack(zombie), "Dead-attacker setup must start.");
            guard.TakeDamage(1000);
            yield return 0.4f;
            Require(zombie.CurrentHealth == hp, "A dead attacker cannot finish its swing.");

            // A newly infected human is no longer a valid melee victim, even before conversion.
            zombie.transform.position = Vector3.zero;
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                new Vector3(0, 0, 0.8f), Quaternion.identity).GetComponent<Civilian>();
            Require(zombie.TryAttack(human.Combat), "Infection-cancellation setup must start.");
            Require(human.TryInfect(AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath), manager.Necromancer, 2),
                "Target must accept infection during windup.");
            yield return zombie.MeleeDuration + 0.05f;
            Require(human && human.Combat.CurrentHealth == 30, "A pending swing must spare an infected civilian.");
            Object.Destroy(human.gameObject);
            yield return zombie.AttackCooldown;
            var attackers = new[] { zombie, heavy, skeleton };
            var targets = new UnitCombat[3];
            var limbs = new Transform[3];
            var resting = new Vector3[3];
            for (int i = 0; i < attackers.Length; i++)
            {
                attackers[i].transform.position = new Vector3((i - 1) * 2, 0, 0);
                targets[i] = Spawn("Guard", attackers[i].transform.position + Vector3.forward * 0.8f);
                limbs[i] = attackers[i].transform.Find(i == 0 ? "Arm_L" : i == 1 ? "Sword" : "Arm_-1");
                resting[i] = limbs[i].localPosition;
                Require(attackers[i].TryAttack(targets[i]), "Each undead type must start a real swing.");
            }
            yield return 0.1f;
            for (int i = 0; i < attackers.Length; i++)
                Require(limbs[i].localPosition != resting[i] && targets[i].CurrentHealth == 100,
                    "Zombie, heavy and skeleton must all visibly wind up before damage.");
            Capture(manager.LevelCamera, "melee-undead-windup");
            yield return 0.25f;
            for (int i = 0; i < attackers.Length; i++)
                Require(targets[i].CurrentHealth == 100 - attackers[i].Damage, "Each undead contact deals its own damage once.");
            Capture(manager.LevelCamera, "melee-undead-contact");
            yield return 0.4f;
            for (int i = 0; i < attackers.Length; i++)
            {
                Require(limbs[i].localPosition == resting[i], "Every undead type returns to its authored pose.");
                Object.Destroy(targets[i].gameObject);
            }
            Object.Destroy(zombie.gameObject);
            Object.Destroy(heavy.gameObject);
            Object.Destroy(skeleton.gameObject);
            yield return 0.1f;
            manager.Necromancer.transform.position = new Vector3(0, 0, -7);
            var pursuer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath),
                new Vector3(0, 0, -6), Quaternion.identity);
            pursuer.Initialize(manager.Necromancer);
            var fleeing = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                new Vector3(0, 0, -5.2f), Quaternion.identity).GetComponent<Civilian>();
            yield return 1.5f;
            Require(!fleeing || fleeing.Combat.CurrentHealth < 30,
                "A zombie must land a real delayed hit on a fleeing civilian in open ground.");
            Object.Destroy(pursuer.gameObject);
            if (fleeing) Object.Destroy(fleeing.gameObject);
            Debug.Log("PASS: melee poses/timings, contact damage/audio/recoil, recovery, pause, range, disable, death and infection cancellation.");
        }

        private static UnitCombat Spawn(string name, Vector3 position)
        {
            var unit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab"),
                position, Quaternion.identity);
            if (unit.TryGetComponent<Guard>(out var guard)) guard.enabled = false;
            if (unit.TryGetComponent<Zombie>(out var zombie)) zombie.enabled = false;
            return unit.GetComponent<UnitCombat>();
        }

        private static void Capture(Camera source, string filename = "melee-contact")
        {
            var camera = new GameObject("MeleeCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 3;
                camera.transform.position = new Vector3(4, 8, -6);
                camera.transform.LookAt(new Vector3(0, 0.5f, 0));
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/" + filename + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target); Object.Destroy(texture); Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
