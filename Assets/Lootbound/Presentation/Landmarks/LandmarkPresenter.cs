using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Presentation.Landmarks
{
    /// <summary>
    /// Renders the world's landmarks - the first presentation layer observing
    /// the LandmarkDirector. Same decoupling as the audio and wildlife
    /// presenters: Gameplay never references this assembly. The presenter
    /// resolves each landmark's DefinitionId through the LandmarkRegistry to
    /// obtain the prefab (the identity itself carries no ScriptableObject).
    ///
    /// A landmark without a prefab shows nothing, unless the development
    /// fallback is explicitly enabled - a dev tool, never a silent data fix.
    /// </summary>
    public sealed class LandmarkPresenter : MonoBehaviour
    {
        private const string RootName = "Landmarks_Presented";

        [Header("Sources")]
        [SerializeField] private LandmarkDirector director;
        [SerializeField] private LandmarkRegistry registry;

        [Header("Development")]
        [SerializeField]
        [Tooltip("OFF by default. When a landmark's definition has no prefab, show a development silhouette instead of nothing. A dev tool, never a silent data fix.")]
        private bool enableDevelopmentFallback;

        private readonly Dictionary<LandmarkIdentity, GameObject> visuals =
            new Dictionary<LandmarkIdentity, GameObject>();

        private LandmarkDirector boundDirector;
        private Transform rootTransform;
        private Material fallbackMaterial;
        private bool fallbackMaterialFailed;

        /// <summary>Number of landmark visuals currently instantiated (tests and inspection).</summary>
        public int ActiveVisualCount => visuals.Count;

        private void OnEnable()
        {
            BindDirector(director);
        }

        private void OnDisable()
        {
            UnbindDirector();
            ReleaseAll();
        }

        private void OnDestroy()
        {
            ReleaseAll();

            if (fallbackMaterial != null)
            {
                Destroy(fallbackMaterial);
                fallbackMaterial = null;
            }
        }

        /// <summary>Rebind to a director at runtime (releases everything first).</summary>
        public void SetDirector(LandmarkDirector newDirector)
        {
            UnbindDirector();
            ReleaseAll();

            director = newDirector;
            if (isActiveAndEnabled)
            {
                BindDirector(newDirector);
            }
        }

        private void BindDirector(LandmarkDirector target)
        {
            if (target == null || boundDirector == target)
            {
                return;
            }

            boundDirector = target;
            boundDirector.OnLandmarkRegistered += HandleRegistered;
            boundDirector.OnLandmarkReleased += HandleReleased;

            // Give a visual to landmarks already present (re-enable): only the
            // missing ones, never a duplicate.
            foreach (var landmark in target.ActiveLandmarks)
            {
                HandleRegistered(landmark);
            }
        }

        private void UnbindDirector()
        {
            if (boundDirector == null)
            {
                return;
            }

            boundDirector.OnLandmarkRegistered -= HandleRegistered;
            boundDirector.OnLandmarkReleased -= HandleReleased;
            boundDirector = null;
        }

        private void HandleRegistered(LandmarkIdentity landmark)
        {
            if (landmark == null || visuals.ContainsKey(landmark))
            {
                return;
            }

            GameObject prefab = ResolvePrefab(landmark.DefinitionId);

            GameObject visual;
            if (prefab != null)
            {
                visual = Instantiate(prefab, landmark.Position, Quaternion.identity, EnsureRoot());
                visual.name = $"Landmark_{landmark.DefinitionId}_{landmark.HostNodeId}";
            }
            else if (enableDevelopmentFallback && TryEnsureFallbackMaterial())
            {
                visual = CreateFallbackSilhouette(landmark);
            }
            else
            {
                // No prefab, no fallback: nothing to show for this landmark.
                return;
            }

            visuals.Add(landmark, visual);
        }

        private void HandleReleased(LandmarkIdentity landmark)
        {
            if (landmark == null || !visuals.TryGetValue(landmark, out var visual))
            {
                return;
            }

            if (visual != null)
            {
                Destroy(visual);
            }

            visuals.Remove(landmark);
        }

        private GameObject ResolvePrefab(string definitionId)
        {
            if (registry == null)
            {
                return null;
            }

            foreach (var definition in registry.Definitions)
            {
                if (definition != null && definition.LandmarkId == definitionId)
                {
                    return definition.LandmarkPrefab;
                }
            }

            return null;
        }

        /// <summary>Development silhouette: a tall primitive sharing ONE presenter-owned material.</summary>
        private GameObject CreateFallbackSilhouette(LandmarkIdentity landmark)
        {
            var silhouette = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            silhouette.name = $"Landmark_DEV_{landmark.DefinitionId}_{landmark.HostNodeId}";

            var collider = silhouette.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = silhouette.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = fallbackMaterial;
            }

            silhouette.transform.SetParent(EnsureRoot(), false);
            silhouette.transform.localScale = new Vector3(1.5f, 4f, 1.5f);
            // Cylinder pivot is its center; height = 2 * scaleY.
            silhouette.transform.position = landmark.Position + Vector3.up * 4f;
            return silhouette;
        }

        /// <summary>
        /// Lazily creates the single fallback material owned by THIS presenter
        /// (shared by its silhouettes, destroyed by it). Defensive shader
        /// lookup; returns false when no compatible shader exists.
        /// </summary>
        private bool TryEnsureFallbackMaterial()
        {
            if (fallbackMaterial != null) return true;
            if (fallbackMaterialFailed) return false;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader == null)
            {
                fallbackMaterialFailed = true;
                return false;
            }

            fallbackMaterial = new Material(shader)
            {
                name = "LandmarkFallback_RuntimeMaterial",
                color = new Color(0.55f, 0.45f, 0.32f)
            };
            return true;
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

        private void ReleaseAll()
        {
            foreach (var visual in visuals.Values)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }

            visuals.Clear();

            if (rootTransform != null)
            {
                Destroy(rootTransform.gameObject);
                rootTransform = null;
            }
        }
    }
}
