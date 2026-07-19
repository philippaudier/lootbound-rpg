using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>
    /// Pure weighted selection of ambient event profiles.
    ///
    /// The activity/selection formula, applied EXACTLY once per profile:
    ///
    ///   effectiveWeight = profile.Weight
    ///                   * Clamp01(profile.ActivityResponse(activity(category)))
    ///
    ///   attemptChance   = Clamp01(baseChancePerEvaluation * Min(1, sum of effectiveWeights))
    ///
    /// One roll against attemptChance decides whether this evaluation spawns
    /// anything at all; a second roll picks the profile proportionally to
    /// the effective weights. No other activity multiplier exists anywhere.
    /// </summary>
    public static class AmbientEventSelector
    {
        /// <summary>The activity intent driving a category (Environmental shares the rare intent).</summary>
        public static float GetActivity(in WorldAmbienceState state, AmbientEventCategory category)
        {
            switch (category)
            {
                case AmbientEventCategory.Birds: return state.BirdActivity;
                case AmbientEventCategory.Insects: return state.InsectActivity;
                case AmbientEventCategory.Wind: return state.WindActivity;
                case AmbientEventCategory.Environmental: return state.RareEventActivity;
                case AmbientEventCategory.Rare: return state.RareEventActivity;
                default: return 0f;
            }
        }

        /// <summary>Weight x clamped activity response - the single activity application.</summary>
        public static float EffectiveWeight(AmbientEventProfile profile, in WorldAmbienceState state)
        {
            if (profile == null || profile.Weight <= 0f)
            {
                return 0f;
            }

            return profile.Weight * profile.EvaluateResponse(GetActivity(state, profile.Category));
        }

        /// <summary>
        /// Attempts one selection. Returns false when nothing is eligible or
        /// the attempt roll fails. <paramref name="hadEligibleProfiles"/>
        /// distinguishes "nothing could spawn" from "chance said no".
        /// Deterministic for a given random sequence.
        /// </summary>
        public static bool TrySelect(
            IReadOnlyList<AmbientEventProfile> profiles,
            in WorldAmbienceState state,
            Func<AmbientEventProfile, bool> isEligible,
            float baseChancePerEvaluation,
            System.Random random,
            out AmbientEventProfile selected,
            out bool hadEligibleProfiles)
        {
            selected = null;
            hadEligibleProfiles = false;

            if (profiles == null || profiles.Count == 0 || random == null)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null || (isEligible != null && !isEligible(profile)))
                {
                    continue;
                }

                float effective = EffectiveWeight(profile, state);
                if (effective > 0f)
                {
                    hadEligibleProfiles = true;
                    totalWeight += effective;
                }
            }

            if (!hadEligibleProfiles)
            {
                return false;
            }

            float attemptChance = Mathf.Clamp01(
                Mathf.Clamp01(baseChancePerEvaluation) * Mathf.Min(1f, totalWeight));
            if (random.NextDouble() >= attemptChance)
            {
                return false;
            }

            float pick = (float)(random.NextDouble() * totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null || (isEligible != null && !isEligible(profile)))
                {
                    continue;
                }

                float effective = EffectiveWeight(profile, state);
                if (effective <= 0f)
                {
                    continue;
                }

                accumulated += effective;
                if (pick <= accumulated)
                {
                    selected = profile;
                    return true;
                }
            }

            // Floating point tail: fall back to the last positive entry.
            for (int i = profiles.Count - 1; i >= 0; i--)
            {
                var profile = profiles[i];
                if (profile != null && (isEligible == null || isEligible(profile)) &&
                    EffectiveWeight(profile, state) > 0f)
                {
                    selected = profile;
                    return true;
                }
            }

            return false;
        }
    }
}
