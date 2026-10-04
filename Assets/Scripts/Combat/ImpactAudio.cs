using UnityEngine;

namespace HaoxiKaiyan
{
    // Tiny procedural placeholders; audio stays local and can be swapped for licensed recordings.
    public sealed class ImpactAudio : MonoBehaviour
    {
        private AudioSource speaker;
        private AudioClip[] clips;

        private void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0f;
            speaker.volume = .52f;
            clips = new AudioClip[3];
            clips[0] = Create("Soft wood tap", .12f, 220f, .22f, 47);
            clips[1] = Create("Heavy wooden strike", .19f, 133f, .40f, 71);
            clips[2] = Create("Wooden launch strike", .27f, 92f, .52f, 97);
        }

        public void Play(HitTier tier)
        {
            if (speaker != null && clips != null) speaker.PlayOneShot(clips[(int)tier]);
        }

        private static AudioClip Create(string name, float seconds, float fundamental, float noiseMix, int seed)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(seconds * sampleRate);
            float[] samples = new float[count];
            var random = new System.Random(seed);
            float previous = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * (name.Contains("Soft") ? 30f : 18f));
                float noise = (float)random.NextDouble() * 2f - 1f;
                previous = previous * .74f + noise * .26f;
                float wood = Mathf.Sin(2f * Mathf.PI * fundamental * t) +
                           .23f * Mathf.Sin(2f * Mathf.PI * fundamental * 2.2f * t);
                samples[i] = Mathf.Clamp((wood * (1f - noiseMix) + previous * noiseMix) * decay * .57f, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
