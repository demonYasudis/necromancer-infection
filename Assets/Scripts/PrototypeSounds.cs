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

        public static AudioClip CreateSoundtrack()
        {
            const float phrase = 8;
            const float duration = phrase * 4;
            // D minor, B-flat, G minor, A. Keep oscillator phases continuous at the loop point.
            float[] bass = { 73.42f, 58.27f, 49f, 55f };
            float[] upper = { 174.61f, 146.83f, 116.54f, 138.59f };
            for (int i = 0; i < bass.Length; i++)
            {
                bass[i] = Mathf.Round(bass[i] * duration) / duration;
                upper[i] = Mathf.Round(upper[i] * duration) / duration;
            }
            return Generate("Dusk on the Road", duration, t =>
            {
                int chord = (int)(t / phrase);
                int previous = (chord + 3) % bass.Length;
                float local = t % phrase;
                float blend = Mathf.SmoothStep(0, 1, local / 2.5f);
                float pad = Mathf.Lerp(Pad(t, bass[previous], upper[previous]), Pad(t, bass[chord], upper[chord]), blend);
                float toll = Mathf.Min(1, local * 3) * Mathf.Exp(-local * 1.5f);
                return pad * (.82f + .18f * Sin(t / phrase))
                    + toll * (.023f * Sin(2 * upper[chord] * local) + .009f * Sin(4 * upper[chord] * local));
            }, false);
        }

        private static float Pad(float time, float bass, float upper) =>
            .065f * Sin(bass * time) + .025f * Sin(2 * bass * time)
            + .019f * Sin(upper * time) + .011f * Sin((upper + .25f) * time);

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
