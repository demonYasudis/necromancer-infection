using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class AudioChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Audio checks need a clean attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager);
            var audio = manager.GetComponent<GameAudio>();
            var leader = manager.Necromancer;
            leader.transform.position = Vector3.zero;
            Require(audio && audio.GetComponentsInChildren<AudioSource>().Length == 11,
                "Every authored scene must automatically have exactly eleven pooled audio sources.");
            SavePreview(audio);
            Debug.Log("PASS: all six procedural clips contain audible, unclipped PCM; corruption loop has a smooth seam; WAV preview exported.");

            var prefab = AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath);
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                new Vector3(-4, 0, 0), Quaternion.identity).GetComponent<Civilian>();
            Physics.SyncTransforms();
            int conversions = audio.PlayedCount(GameAudio.Cue.Conversion);
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            Require(ability.TryInfectAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(human.transform.position + Vector3.up * 0.75f)),
                "Actual paid infection must start.");
            yield return ability.InfectionDelay + 0.2f;
            Require(!human && audio.PlayedCount(GameAudio.Cue.Conversion) == conversions + 1,
                "A real completed infection must sound once.");
            var attacker = Object.FindFirstObjectByType<Zombie>();
            attacker.enabled = false;
            attacker.transform.position = Vector3.zero;
            var defender = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                new Vector3(0.6f, 0, 0), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false;
            int impacts = audio.PlayedCount(GameAudio.Cue.Impact);
            Require(attacker.Combat.TryAttack(defender.Combat) && audio.PlayedCount(GameAudio.Cue.Impact) == impacts,
                "Windup must not play an impact early.");
            yield return attacker.Combat.MeleeWindup + 0.05f;
            Require(audio.PlayedCount(GameAudio.Cue.Impact) == impacts + 1,
                "A real melee hit must trigger impact audio.");
            Object.Destroy(defender.gameObject);
            Object.Destroy(attacker.gameObject);
            yield return 0.3f;
            int dropped = audio.DroppedSounds;
            impacts = audio.PlayedCount(GameAudio.Cue.Impact);
            for (int i = 0; i < 100; i++) audio.TryPlay(GameAudio.Cue.Impact, leader.transform.position);
            Require(audio.PlayedCount(GameAudio.Cue.Impact) == impacts + 1 && audio.DroppedSounds >= dropped + 99,
                "A same-frame hit burst must be coalesced instead of creating a hundred sources.");
            Debug.Log("PASS: real paid conversion and melee hit trigger their sounds; simultaneous impacts are throttled.");

            int footsteps = audio.PlayedCount(GameAudio.Cue.Footstep);
            for (int i = 0; i < 30; i++)
            {
                float angle = i * Mathf.PI * 2 / 30;
                var unit = Object.Instantiate(prefab, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2, Quaternion.identity);
                unit.Initialize(leader);
            }
            var destination = new GameObject("AudioCheckDestination").transform;
            destination.position = Vector3.right * 4;
            leader.enabled = true;
            leader.BeginRoute(Vector3.zero, new[] { destination });
            for (int tick = 0; tick < 12; tick++)
            {
                yield return 0.15f;
                CheckLimits(audio);
            }
            Require(audio.PlayedCount(GameAudio.Cue.Footstep) > footsteps
                && audio.GetComponentsInChildren<AudioSource>().Length == 11,
                "Thirty moving zombies must make footsteps without growing the audio pool.");
            Debug.Log("PASS: thirty real moving zombies produce footsteps within fixed per-type and total voice limits.");
            Prepare(manager);
            foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) zombie.gameObject.SetActive(false);
            var bell = Object.FindFirstObjectByType<AlarmBell>();
            var runner = bell.Runner;
            runner.gameObject.SetActive(true);
            runner.transform.position = bell.ArrivalPoint.position + Vector3.right * 2;
            leader.transform.position = runner.transform.position + Vector3.left;
            Require(runner.Tick() && runner.IsRunning, "Messenger must detect undead before ringing.");
            runner.transform.position = bell.ArrivalPoint.position;
            int bells = audio.PlayedCount(GameAudio.Cue.Bell);
            Require(bell.TryRaise(runner) && audio.PlayedCount(GameAudio.Cue.Bell) == bells + 1,
                "Actual bell arrival must immediately ring through its reserved voice.");
            foreach (var guard in bell.Patrol.GetComponentsInChildren<Guard>()) guard.enabled = false;
            runner.gameObject.SetActive(false);
            Require(!bell.TryRaise(runner) && audio.PlayedCount(GameAudio.Cue.Bell) == bells + 1,
                "Repeated alarm requests must not ring twice.");
            Time.timeScale = 0;
            yield return 0.2f;
            Require(audio.IsPaused && audio.ActiveVoices(GameAudio.Cue.Bell) == 1
                && !audio.TryPlay(GameAudio.Cue.Impact, leader.transform.position),
                "Pause must preserve the bell voice and reject new sounds.");
            Time.timeScale = 1;
            yield return 0.15f;
            Require(!audio.IsPaused && audio.PlayedCount(GameAudio.Cue.Bell) == bells + 1,
                "Unpause must resume the bell without replaying it.");
            audio.enabled = false;
            Require(Silent(audio), "Disabling audio must stop every source.");
            audio.enabled = true;
            yield return 0.1f;
            impacts = audio.PlayedCount(GameAudio.Cue.Impact);
            Require(leader.Combat.TakeDamage(1) && audio.PlayedCount(GameAudio.Cue.Impact) == impacts + 1,
                "Re-enabling audio must subscribe to actual damage exactly once.");
            Debug.Log("PASS: actual alarm rings once; pause/resume preserves voices; disabling/re-enabling clears sources and avoids duplicate subscriptions.");

            Prepare(manager);
            var farm = manager.Farm;
            var assault = farm.GetComponent<FarmAssault>();
            // Isolate the corruption sound from combat; spawning and wave cues remain real.
            assault.WaveArrived += _ => {
                foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>()) guard.enabled = false;
            };
            leader.enabled = true;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            yield return 0.25f;
            Require(audio.IsCorrupting, "Actual farm corruption must start the looping drone.");
            float volume = audio.CorruptionVolume, pitch = audio.CorruptionPitch;
            yield return 3.2f;
            Require(audio.CorruptionVolume > volume && audio.CorruptionPitch > pitch
                && audio.PlayedCount(GameAudio.Cue.Wave) == 1,
                "Corruption must grow in level/pitch, and the first real reinforcement wave must sound.");
            leader.transform.position = farm.transform.position + Vector3.left * (farm.ActivationRadius + 1);
            leader.enabled = false;
            yield return 0.1f;
            Require(!audio.IsCorrupting && audio.CorruptionVolume == 0,
                "Leaving the capture radius must stop corruption audio.");
            leader.enabled = true;
            leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
            var settings = new SerializedObject(assault);
            settings.FindProperty("corruptionDuration").floatValue = 0.5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return 0.75f;
            Require(manager.IsVictorious && Silent(audio), "Victory must stop all one-shots and the loop.");
            Time.timeScale = 8;
            yield return 0.2f;
            Require(Silent(audio) && !audio.TryPlay(GameAudio.Cue.Bell, leader.transform.position),
                "Finished attempts must remain silent even if time resumes.");
            Require(manager.TryRestartLevel(), "Audio attempt must restart after victory.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager);
            audio = manager.GetComponent<GameAudio>();
            Require(!audio.IsPaused && !audio.IsCorrupting && audio.PlayedCount(GameAudio.Cue.Bell) == 0
                && audio.PlayedCount(GameAudio.Cue.Wave) == 0
                && Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 11,
                "Retry must discard old clips/sources, reset counts and create only one fresh audio pool.");
            manager.Necromancer.Combat.TakeDamage(1000);
            yield return 0.1f;
            Require(manager.IsDefeated && Silent(audio), "Defeat must also silence audio.");
            Require(manager.TryRestartLevel(), "Defeat must allow retry.");
            yield return 0.35f;
            Require(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 11,
                "Repeated scene reloads must not leak audio sources.");
            Debug.Log("PASS: real corruption grows, resets on exit and signals waves; victory/defeat stop all audio; both retries reset the bounded pool.");
        }

        private static bool Silent(GameAudio audio)
        {
            if (audio.IsCorrupting) return false;
            foreach (GameAudio.Cue cue in Enum.GetValues(typeof(GameAudio.Cue)))
                if (audio.ActiveVoices(cue) != 0) return false;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>()) if (source.isPlaying) return false;
            return true;
        }
        private static void CheckLimits(GameAudio audio)
        {
            foreach (GameAudio.Cue cue in Enum.GetValues(typeof(GameAudio.Cue)))
                Require(audio.ActiveVoices(cue) <= GameAudio.VoiceLimit(cue), "Audio exceeded its per-type voice budget.");
        }
        private static void Prepare(GameManager manager)
        {
            manager.Necromancer.enabled = false;
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
        }
        private static void SavePreview(GameAudio audio)
        {
            var samples = new System.Collections.Generic.List<float>();
            for (int index = 0; index < 6; index++)
            {
                var clip = index < 5 ? audio.Clip((GameAudio.Cue)index) : audio.CorruptionClip;
                var data = new float[clip.samples];
                Require(clip.GetData(data, 0), "Procedural PCM must be readable.");
                float peak = 0, energy = 0;
                foreach (float value in data) { peak = Mathf.Max(peak, Mathf.Abs(value)); energy += value * value; }
                Require(peak > 0.1f && peak <= 0.951f && energy / data.Length > 0.0001f,
                    "Sound must have usable level without clipping or silence.");
                if (index == 5) Require(Mathf.Abs(data[0] - data[data.Length - 1]) < 0.03f, "Loop seam must be smooth.");
                foreach (float value in data) samples.Add(value * 0.65f);
                samples.AddRange(new float[PrototypeSounds.SampleRate / 3]);
            }
            Directory.CreateDirectory("Logs");
            using (var writer = new BinaryWriter(File.Create("Logs/task22-sound-preview.wav")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(PrototypeSounds.SampleRate);
                writer.Write(PrototypeSounds.SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Count * 2);
                foreach (float value in samples) writer.Write((short)(value * short.MaxValue));
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
