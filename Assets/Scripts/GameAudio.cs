using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Scene-owned bounded audio pool. Important cues never compete with crowd voices.</summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        public enum Cue { Footstep, Impact, Conversion, Bell, Wave }
        private static readonly int[] Limits = { 3, 3, 2, 1, 1 };
        private static readonly float[] Volumes = { 0.14f, 0.28f, 0.48f, 0.65f, 0.45f };
        private static readonly float[] Intervals = { 0.1f, 0.065f, 0.12f, 0.5f, 0.5f };
        [SerializeField, Range(0, 1)] private float masterVolume = 0.8f;
        private sealed class Voice
        {
            public Cue cue;
            public AudioSource source;
            public double endsAt;
            public float gain;
        }
        private sealed class Walker
        {
            public UnitCombat unit;
            public Vector3 previous;
            public float distance;
        }
        private readonly List<Voice> voices = new List<Voice>();
        private readonly List<Walker> walkers = new List<Walker>();
        private readonly AudioClip[] clips = new AudioClip[5];
        private readonly float[] nextAllowed = new float[5];
        private readonly int[] played = new int[5];
        private GameManager manager;
        private FarmAssault assault;
        private AudioSource corruption;
        private Transform audioRoot;
        private float nextStepSample;
        private double pausedAt;
        private bool finished;

        public bool IsPaused { get; private set; }
        public bool IsCorrupting => corruption && corruption.clip && corruption.volume > 0;
        public float CorruptionVolume => corruption ? corruption.volume : 0;
        public float CorruptionPitch => corruption ? corruption.pitch : 0;
        public int DroppedSounds { get; private set; }
        public int PlayedCount(Cue cue) => played[(int)cue];
        public AudioClip Clip(Cue cue) => clips[(int)cue];
        public AudioClip CorruptionClip => corruption ? corruption.clip : null;
        public static int VoiceLimit(Cue cue) => Limits[(int)cue];
        public int ActiveVoices(Cue cue)
        {
            int count = 0;
            double now = IsPaused ? pausedAt : Time.unscaledTimeAsDouble;
            foreach (var voice in voices) if (voice.cue == cue && voice.endsAt > now) count++;
            return count;
        }

        private void Awake()
        {
            manager = GetComponent<GameManager>();
            audioRoot = new GameObject("GameAudioSources").transform;
            audioRoot.SetParent(transform, false);
            for (int type = 0; type < clips.Length; type++)
            {
                clips[type] = PrototypeSounds.Create((Cue)type);
                for (int i = 0; i < Limits[type]; i++)
                    voices.Add(new Voice { cue = (Cue)type, source = CreateSource(((Cue)type).ToString(),
                        type < 2 ? 220 - type * 30 : type == 3 ? 32 : 80) });
            }
            corruption = CreateSource("Corruption", 96);
            corruption.clip = PrototypeSounds.CreateCorruption();
            corruption.loop = true;
            corruption.volume = 0;
        }

        private AudioSource CreateSource(string label, int priority)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(audioRoot, false);
            source.playOnAwake = false;
            source.spatialBlend = 0; // Mix relative to the horde, independent of the overhead camera height.
            source.priority = priority;
            source.dopplerLevel = 0;
            return source;
        }

        private void OnEnable()
        {
            UnitCombat.AnyDamaged += OnDamaged;
            Zombie.Raised += OnRaised;
            AlarmBell.Rang += OnBell;
            assault = manager && manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            if (assault) assault.WaveArrived += OnWave;
            foreach (var undead in FindObjectsByType<UndeadPresence>(FindObjectsSortMode.None))
                if (undead.gameObject.scene == gameObject.scene) Track(undead.GetComponent<UnitCombat>());
        }

        private void OnDisable()
        {
            UnitCombat.AnyDamaged -= OnDamaged;
            Zombie.Raised -= OnRaised;
            AlarmBell.Rang -= OnBell;
            if (assault) assault.WaveArrived -= OnWave;
            StopAll();
            walkers.Clear();
        }

        private void OnDestroy()
        {
            foreach (var clip in clips) if (clip) Destroy(clip);
            if (corruption && corruption.clip) Destroy(corruption.clip);
            if (audioRoot) Destroy(audioRoot.gameObject);
        }

        private bool CanPlay => isActiveAndEnabled && manager && !manager.IsFinished && !manager.IsRestarting
            && Time.timeScale > 0 && !IsPaused;

        private void OnDamaged(UnitCombat victim)
        {
            if (victim && victim.gameObject.scene == gameObject.scene) TryPlay(Cue.Impact, victim.transform.position);
        }
        private void OnRaised(Zombie zombie)
        {
            if (!zombie || zombie.gameObject.scene != gameObject.scene) return;
            Track(zombie.Combat);
            TryPlay(Cue.Conversion, zombie.transform.position);
        }
        private void OnBell(AlarmBell bell)
        {
            if (bell && bell.gameObject.scene == gameObject.scene) TryPlay(Cue.Bell, bell.transform.position);
        }
        private void OnWave(int number) => TryPlay(Cue.Wave, assault.transform.position);

        private void Track(UnitCombat unit)
        {
            if (!unit || walkers.Exists(walker => walker.unit == unit)) return;
            walkers.Add(new Walker { unit = unit, previous = unit.transform.position });
        }

        public bool TryPlay(Cue cue, Vector3 position)
        {
            if (!CanPlay || masterVolume <= 0) return false;
            int type = (int)cue;
            double now = Time.unscaledTimeAsDouble;
            if (Time.time < nextAllowed[type]) { DroppedSounds++; return false; }
            Vector3 offset = manager.Necromancer ? position - manager.Necromancer.transform.position : Vector3.zero;
            float gain = Volumes[type];
            if ((int)cue < (int)Cue.Bell)
            {
                gain *= Mathf.Clamp01(1 - offset.magnitude / 20);
                if (gain <= 0.001f) return false;
            }
            foreach (var voice in voices)
            {
                if (voice.cue != cue || voice.endsAt > now) continue;
                voice.source.Stop();
                voice.source.clip = clips[type];
                // Deterministic variation without affecting the game's random decisions.
                voice.source.pitch = (int)cue < (int)Cue.Bell ? 0.94f + played[type] % 5 * 0.03f : 1;
                voice.source.panStereo = (int)cue >= (int)Cue.Bell ? 0 : Mathf.Clamp(Vector3.Dot(offset,
                    manager.LevelCamera ? manager.LevelCamera.transform.right : Vector3.right) / 12, -0.65f, 0.65f);
                voice.gain = gain;
                voice.endsAt = now + clips[type].length / voice.source.pitch;
                nextAllowed[type] = Time.time + Intervals[type];
                played[type]++;
                voice.source.volume = gain * masterVolume * Duck(cue);
                voice.source.Play();
                return true;
            }
            DroppedSounds++;
            return false;
        }

        private float Duck(Cue cue) => (int)cue < (int)Cue.Bell && (ActiveVoices(Cue.Bell) > 0 || ActiveVoices(Cue.Wave) > 0)
            ? 0.35f : (int)cue < (int)Cue.Conversion && ActiveVoices(Cue.Conversion) > 0 ? 0.6f : 1;

        private void LateUpdate()
        {
            if (!manager || manager.IsFinished || manager.IsRestarting)
            {
                if (!finished) { StopAll(); finished = true; }
                return;
            }
            bool paused = Time.timeScale <= 0;
            if (paused != IsPaused)
            {
                if (paused)
                {
                    pausedAt = Time.unscaledTimeAsDouble;
                    foreach (var voice in voices) voice.source.Pause();
                    corruption.Pause();
                }
                else
                {
                    double delay = Time.unscaledTimeAsDouble - pausedAt;
                    foreach (var voice in voices)
                    {
                        if (voice.endsAt <= pausedAt) continue;
                        voice.endsAt += delay;
                        voice.source.UnPause();
                    }
                    corruption.UnPause();
                }
                IsPaused = paused;
            }
            if (paused) return;
            foreach (var voice in voices) voice.source.volume = voice.gain * masterVolume * Duck(voice.cue);
            UpdateCorruption();
            if (Time.time < nextStepSample) return;
            nextStepSample = Time.time + 0.1f;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var walker = walkers[i];
                if (!walker.unit) { walkers.RemoveAt(i); continue; }
                Vector3 position = walker.unit.transform.position;
                float distance = Vector3.Distance(position, walker.previous);
                walker.previous = position;
                if (!walker.unit.isActiveAndEnabled || !walker.unit.IsAlive || distance > 2)
                { walker.distance = 0; continue; }
                walker.distance += distance;
                if (walker.distance < 0.65f) continue;
                walker.distance %= 0.65f;
                TryPlay(Cue.Footstep, position);
            }
        }

        private void UpdateCorruption()
        {
            var leader = manager.Necromancer;
            bool active = assault && assault.isActiveAndEnabled && manager.Farm.isActiveAndEnabled
                && assault.HasStarted && assault.CorruptionSeconds > 0 && !assault.IsCaptured
                && leader && leader.isActiveAndEnabled && leader.Combat.IsAlive;
            if (active)
            {
                Vector3 offset = leader.transform.position - manager.Farm.transform.position;
                offset.y = 0;
                active = offset.sqrMagnitude <= manager.Farm.ActivationRadius * manager.Farm.ActivationRadius;
            }
            if (!active) { corruption.Stop(); corruption.volume = 0; return; }
            corruption.volume = masterVolume * Mathf.Lerp(0.07f, 0.32f, assault.Progress);
            corruption.pitch = Mathf.Lerp(0.75f, 1.45f, assault.Progress);
            if (!corruption.isPlaying) corruption.Play();
        }

        public void StopAll()
        {
            foreach (var voice in voices) { voice.source.Stop(); voice.endsAt = 0; }
            if (corruption) { corruption.Stop(); corruption.volume = 0; }
            IsPaused = false;
            System.Array.Clear(nextAllowed, 0, nextAllowed.Length);
        }
    }
}
