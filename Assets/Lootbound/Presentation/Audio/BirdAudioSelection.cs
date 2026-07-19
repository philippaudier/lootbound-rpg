using UnityEngine;

namespace Lootbound.Presentation.Audio
{
    /// <summary>
    /// Pure, deterministic selection helpers for the bird voice. Null clip
    /// entries are always ignored; nothing here ever touches the global
    /// UnityEngine.Random state.
    /// </summary>
    public static class BirdAudioSelection
    {
        /// <summary>True when at least one non-null clip exists.</summary>
        public static bool HasValidClip(AudioClip[] clips)
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null) return true;
            }

            return false;
        }

        /// <summary>
        /// Picks a random clip among the NON-NULL entries only. Returns false
        /// for a null/empty/all-null array.
        /// </summary>
        public static bool TryPickClip(AudioClip[] clips, System.Random random, out AudioClip clip)
        {
            clip = null;
            if (clips == null || random == null) return false;

            int validCount = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null) validCount++;
            }

            if (validCount == 0) return false;

            int pick = random.Next(validCount);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                if (pick == 0)
                {
                    clip = clips[i];
                    return true;
                }

                pick--;
            }

            return false;
        }

        /// <summary>Uniform value in an (unordered) range.</summary>
        public static float PickInRange(Vector2 range, System.Random random)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            if (random == null) return min;
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }
    }
}
