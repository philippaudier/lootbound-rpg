using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>
    /// One live flock: a root GameObject, its bird transforms and the plan
    /// that drives them. Pure runtime class - no MonoBehaviour, no
    /// per-bird Update: the presenter ticks every flock centrally.
    /// </summary>
    public sealed class BirdFlockRuntime
    {
        private const float BobFrequency = 2.1f;
        private const float FlapScaleAmount = 0.35f;

        private struct BirdVisual
        {
            public Transform Transform;
            public Vector3 BaseScale;
        }

        private readonly List<BirdVisual> birds = new List<BirdVisual>();
        private readonly BirdFlockPlan plan;
        private readonly bool animateFallbackFlap;

        private float elapsed;
        private bool released;

        public GameObject Root { get; }
        public bool IsReleased => released;
        public int ActiveBirdCount => released ? 0 : birds.Count;

        /// <summary>True once the last bird has finished the path.</summary>
        public bool IsFinished => elapsed >= plan.TotalDuration;

        public BirdFlockRuntime(GameObject root, BirdFlockPlan plan, bool animateFallbackFlap)
        {
            Root = root;
            this.plan = plan;
            this.animateFallbackFlap = animateFallbackFlap;
        }

        public void AddBird(Transform birdTransform, float scale)
        {
            if (birdTransform == null) return;

            birds.Add(new BirdVisual
            {
                Transform = birdTransform,
                BaseScale = Vector3.one * scale
            });
        }

        /// <summary>
        /// Advances the whole flock. Framerate independent: progression is
        /// elapsed/duration per bird; start delays never produce a negative
        /// t (clamped to the path start).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (released) return;

            elapsed += deltaTime;

            for (int i = 0; i < birds.Count; i++)
            {
                var bird = birds[i];
                if (bird.Transform == null) continue;

                var spec = plan.Specs[i];

                float local = Mathf.Max(0f, elapsed - spec.StartDelay);
                float t = plan.Duration > 0f
                    ? Mathf.Clamp01(local * spec.SpeedFactor / plan.Duration)
                    : 1f;

                Vector3 position = plan.Trajectory.Evaluate(t) + spec.FormationOffset;
                position.y += Mathf.Sin(elapsed * BobFrequency + spec.BobPhase) * spec.BobAmplitude;
                bird.Transform.position = position;

                Vector3 direction = plan.Trajectory.EvaluateDirection(t);
                if (direction.sqrMagnitude > 0.000001f)
                {
                    bird.Transform.rotation = Quaternion.LookRotation(direction);
                }

                if (animateFallbackFlap)
                {
                    float flap = 1f + FlapScaleAmount *
                        Mathf.Sin(elapsed * spec.FlapSpeed * Mathf.PI * 2f + spec.BobPhase);
                    var scale = bird.BaseScale;
                    scale.y *= Mathf.Max(0.2f, flap);
                    bird.Transform.localScale = scale;
                }
            }
        }

        /// <summary>Idempotent: destroys the root once, then no-ops.</summary>
        public void Release()
        {
            if (released) return;
            released = true;

            birds.Clear();
            if (Root != null)
            {
                Object.Destroy(Root);
            }
        }
    }
}
