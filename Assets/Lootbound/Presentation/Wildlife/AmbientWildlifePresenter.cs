using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Ambience.Events;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>
    /// First visual presentation of the ambient event system: Birds events
    /// grow a small, loose flock crossing, rising or circling around the
    /// event position. Same decoupling as the audio presenter: Gameplay
    /// never references this assembly; this presenter only observes the
    /// director's runtime instances.
    ///
    /// Flock roots live under this presenter (a flight travels world space;
    /// it is never parented to the director-owned marker). When a flight
    /// finishes before its event, the flock is released and the instance is
    /// remembered as completed so rescans never replay it.
    ///
    /// V1 pooling decision: instantiate/destroy. Spawn frequency is low
    /// (at most one director spawn per evaluation tick, a handful of
    /// simultaneous events); a pool would be premature. Release() is the
    /// single exit point where a pool could later be inserted.
    /// </summary>
    public sealed class AmbientWildlifePresenter : MonoBehaviour
    {
        private const string RootName = "AmbientWildlife_Active";
        private const float GroundClearance = 2f;

        [Header("Sources")]
        [SerializeField] private AmbientEventDirector eventDirector;
        [SerializeField] private BirdVisualLibrary birdLibrary;

        [SerializeField]
        [Tooltip("Optional: minimum flight height is sampled from the generated terrain")]
        private ProceduralTerrainGenerator terrainGenerator;

        [SerializeField]
        [Tooltip("Optional: flights starting inside this camera radius are pushed away (global path translation)")]
        private Transform cameraTransform;

        [SerializeField, Min(0f)]
        private float cameraClearRadius = 8f;

        [Header("Development")]
        [SerializeField]
        [Tooltip("OFF by default. When no valid variant exists, allows a development silhouette instead of showing nothing. A dev tool - never a silent data fix.")]
        private bool enableDevelopmentFallback;

        private readonly Dictionary<AmbientEventInstance, BirdFlockRuntime> activeFlocks =
            new Dictionary<AmbientEventInstance, BirdFlockRuntime>();

        // Instances whose flight finished before the event ended: rescans
        // must never replay them. Cleared when the event is released, and
        // pruned against the director's live instances on rebind.
        private readonly HashSet<AmbientEventInstance> completedInstances =
            new HashSet<AmbientEventInstance>();

        private readonly List<AmbientEventInstance> releaseBuffer = new List<AmbientEventInstance>();

        private AmbientEventDirector boundDirector;
        private Transform rootTransform;
        private Material fallbackMaterial;
        private bool fallbackMaterialFailed;

        #region Debug (tests and inspection)

        public int ActiveFlockCount => activeFlocks.Count;

        public int ActiveBirdCount
        {
            get
            {
                int count = 0;
                foreach (var flock in activeFlocks.Values)
                {
                    count += flock.ActiveBirdCount;
                }

                return count;
            }
        }

        public int CompletedInstanceCount => completedInstances.Count;

        #endregion

        private void OnEnable()
        {
            BindDirector(eventDirector);
        }

        private void OnDisable()
        {
            UnbindDirector();
            ReleaseAllFlocks();
            // completedInstances survives disable on purpose: a re-enable
            // while a visually finished event is still active must never
            // recreate a second flight.
        }

        private void OnDestroy()
        {
            ReleaseAllFlocks();
            completedInstances.Clear();

            if (fallbackMaterial != null)
            {
                Destroy(fallbackMaterial);
                fallbackMaterial = null;
            }
        }

        /// <summary>Rebinds to a director at runtime (releases everything first).</summary>
        public void SetDirector(AmbientEventDirector director)
        {
            UnbindDirector();
            ReleaseAllFlocks();
            completedInstances.Clear();

            eventDirector = director;
            if (isActiveAndEnabled)
            {
                BindDirector(director);
            }
        }

        private void BindDirector(AmbientEventDirector director)
        {
            if (director == null || boundDirector == director) return;

            boundDirector = director;
            boundDirector.OnEventSpawned += HandleSpawned;
            boundDirector.OnEventReleased += HandleReleased;

            // Drop tombstones of instances the director no longer knows.
            completedInstances.RemoveWhere(instance => !IsStillActive(director, instance));

            // Give a flight to events already alive (re-enable): only the
            // missing, never-completed ones.
            foreach (var instance in director.ActiveInstances)
            {
                HandleSpawned(instance);
            }
        }

        private void UnbindDirector()
        {
            if (boundDirector == null) return;

            boundDirector.OnEventSpawned -= HandleSpawned;
            boundDirector.OnEventReleased -= HandleReleased;
            boundDirector = null;
        }

        private static bool IsStillActive(AmbientEventDirector director, AmbientEventInstance instance)
        {
            var active = director.ActiveInstances;
            for (int i = 0; i < active.Count; i++)
            {
                if (ReferenceEquals(active[i], instance)) return true;
            }

            return false;
        }

        private void Update()
        {
            if (activeFlocks.Count == 0) return;

            float deltaTime = Time.deltaTime;
            releaseBuffer.Clear();

            foreach (var pair in activeFlocks)
            {
                pair.Value.Tick(deltaTime);
                if (pair.Value.IsFinished)
                {
                    releaseBuffer.Add(pair.Key);
                }
            }

            // Natural end: the birds flew away. Release the presentation and
            // remember the instance so it never replays while still active.
            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                var instance = releaseBuffer[i];
                if (activeFlocks.TryGetValue(instance, out var flock))
                {
                    flock.Release();
                    activeFlocks.Remove(instance);
                    completedInstances.Add(instance);
                }
            }

            releaseBuffer.Clear();
        }

        private void HandleSpawned(AmbientEventInstance instance)
        {
            if (instance == null || instance.Profile == null ||
                instance.Profile.Category != AmbientEventCategory.Birds)
            {
                return;
            }

            if (activeFlocks.ContainsKey(instance) || completedInstances.Contains(instance))
            {
                return;
            }

            // Resolve the pure planner inputs first (stable seed, anchor,
            // minimum height) - the planner itself never touches the scene.
            Vector3 anchor = instance.Position;
            int seed = BirdFlockPlanner.DeriveSeed(instance.Profile.EventId, anchor, instance.SpawnTime);
            float minimumHeight = ResolveMinimumHeight(anchor);

            if (!BirdFlockPlanner.TryCreatePlan(
                    seed, birdLibrary, anchor, minimumHeight, enableDevelopmentFallback, out var plan))
            {
                // No valid variant and no fallback allowed: nothing to show.
                return;
            }

            // Optional camera avoidance: a pure global translation of the
            // path - the plan content is never altered.
            if (cameraTransform != null && cameraClearRadius > 0f)
            {
                Vector3 toStart = plan.Trajectory.Start - cameraTransform.position;
                float distance = toStart.magnitude;
                if (distance < cameraClearRadius)
                {
                    Vector3 push = distance > 0.01f
                        ? toStart / distance
                        : Vector3.forward;
                    plan.Translate(push * (cameraClearRadius - distance));
                }
            }

            var flock = CreateFlock(instance, plan);
            if (flock != null)
            {
                activeFlocks.Add(instance, flock);
            }
        }

        private void HandleReleased(AmbientEventInstance instance)
        {
            if (instance == null) return;

            if (activeFlocks.TryGetValue(instance, out var flock))
            {
                flock.Release();
                activeFlocks.Remove(instance);
            }

            // The event is gone: its completion tombstone is no longer needed.
            completedInstances.Remove(instance);
        }

        private BirdFlockRuntime CreateFlock(AmbientEventInstance instance, BirdFlockPlan plan)
        {
            bool useFallback = plan.VariantIndex < 0;
            GameObject prefab = null;

            if (!useFallback)
            {
                prefab = birdLibrary.Variants[plan.VariantIndex].Prefab;
                if (prefab == null)
                {
                    return null;
                }
            }
            else if (!TryEnsureFallbackMaterial())
            {
                // No compatible shader: abandon the fallback cleanly.
                return null;
            }

            var root = new GameObject($"BirdFlock_{instance.Profile.EventId}");
            root.transform.SetParent(EnsureRoot(), false);
            root.transform.position = instance.Position;

            var flock = new BirdFlockRuntime(root, plan, animateFallbackFlap: useFallback);

            for (int i = 0; i < plan.BirdCount; i++)
            {
                var spec = plan.Specs[i];
                GameObject bird = useFallback
                    ? CreateFallbackBird()
                    : Instantiate(prefab);

                if (bird == null) continue;

                bird.name = $"Bird_{i}";
                bird.transform.SetParent(root.transform, false);
                bird.transform.localScale = Vector3.one * spec.Scale;
                bird.transform.position = plan.Trajectory.Start + spec.FormationOffset;
                flock.AddBird(bird.transform, spec.Scale);
            }

            // First pose immediately (never one frame at origin).
            flock.Tick(0f);
            return flock;
        }

        /// <summary>Development silhouette: a tiny quad sharing ONE presenter-owned material.</summary>
        private GameObject CreateFallbackBird()
        {
            var bird = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var collider = bird.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = bird.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = fallbackMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            bird.transform.localScale = new Vector3(0.6f, 0.25f, 1f);
            return bird;
        }

        /// <summary>
        /// Lazily creates the single fallback material owned by THIS
        /// presenter (shared by its birds, destroyed by it). Defensive
        /// shader lookup; returns false when no compatible shader exists.
        /// </summary>
        private bool TryEnsureFallbackMaterial()
        {
            if (fallbackMaterial != null) return true;
            if (fallbackMaterialFailed) return false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                fallbackMaterialFailed = true;
                return false;
            }

            fallbackMaterial = new Material(shader)
            {
                name = "BirdFallback_RuntimeMaterial",
                color = new Color(0.12f, 0.13f, 0.15f)
            };
            return true;
        }

        private float ResolveMinimumHeight(Vector3 anchor)
        {
            var context = terrainGenerator != null ? terrainGenerator.Context : null;
            if (context != null && context.NormalizedHeightMap != null)
            {
                return context.SampleHeightAtWorld(anchor.x, anchor.z) + GroundClearance;
            }

            return anchor.y;
        }

        private Transform EnsureRoot()
        {
            if (rootTransform == null)
            {
                var root = new GameObject(RootName);
                root.transform.SetParent(transform, false);
                rootTransform = root.transform;
            }

            return rootTransform;
        }

        private void ReleaseAllFlocks()
        {
            foreach (var flock in activeFlocks.Values)
            {
                flock.Release();
            }

            activeFlocks.Clear();
            releaseBuffer.Clear();

            if (rootTransform != null)
            {
                Destroy(rootTransform.gameObject);
                rootTransform = null;
            }
        }
    }
}
