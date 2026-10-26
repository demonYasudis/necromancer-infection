using System;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Original, deterministic prototype sounds; no downloaded assets or gameplay RNG.</summary>
    public static class PrototypeSounds
    {
        public const int SampleRate = 22050;

        public static AudioClip Create(GameAudio.Cue cue)
        {
            float duration = cue == GameAudio.Cue.Footstep ? 0.18f : cue == GameAudio.Cue.Impact ? 0.24f
                : cue == GameAudio.Cue.Conversion ? 1 : cue == GameAudio.Cue.Bell ? 2.8f : 1.1f;
            var random = new System.Random(2200 + (int)cue);
            float filtered = 0;
            return Generate(cue.ToString(), duration, t => {
                float noise = (float)random.NextDouble() * 2 - 1;
                filtered = filtered * 0.8f + noise * 0.2f;
                switch (cue)
                {
                    case GameAudio.Cue.Footstep:
                        return (0.65f * filtered + 0.35f * Sin(85 * t)) * Mathf.Exp(-24 * t);
                    case GameAudio.Cue.Impact:
                        return (0.55f * noise + 0.3f * Sin(150 * t) + 0.15f * Sin(1170 * t)) * Mathf.Exp(-22 * t);
                    case GameAudio.Cue.Conversion:
                        return (0.45f * Sin(130 * t + 235 * t * t) + 0.25f * Sin(65 * t)
                            + 0.3f * filtered) * Mathf.Sin(Mathf.PI * t);
                    case GameAudio.Cue.Bell:
                        return 0.5f * Sin(440 * t) * Mathf.Exp(-1.5f * t)
                            + 0.28f * Sin(1188 * t) * Mathf.Exp(-2.3f * t)
                            + 0.15f * Sin(2380 * t) * Mathf.Exp(-3.5f * t);
                    default:
                        return (0.6f * Sin(140 * t) + 0.3f * Sin(210 * t))
                            * Mathf.Exp(-2 * t) * (0.65f + 0.35f * Sin(3 * t));
                }
            }, true);
        }

        public static AudioClip CreateCorruption() => Generate("Corruption", 2, t =>
            (0.5f * Sin(55 * t) + 0.28f * Sin(82 * t) + 0.15f * Sin(110 * t))
                * (0.8f + 0.2f * Sin(0.5f * t)), false);

        private static float Sin(float cycles) => Mathf.Sin(2 * Mathf.PI * cycles);

        private static AudioClip Generate(string name, float duration, Func<float, float> sample, bool fade)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = fade ? Mathf.Min(1, t / 0.004f, (duration - t) / 0.04f) : 1;
                data[i] = Mathf.Clamp(sample(t) * envelope, -0.95f, 0.95f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
