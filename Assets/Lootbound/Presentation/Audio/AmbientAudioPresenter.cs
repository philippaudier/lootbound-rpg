using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Ambience.Events;

namespace Lootbound.Presentation.Audio
{
    /// <summary>
    /// First presentation layer of the ambient event system: gives the
    /// Birds category a spatial voice. Listens to the director's spawn and
    /// release events and creates a runtime child ("BirdAudioPresentation")
    /// under each bird marker, carrying a 3D AudioSource.
    ///
    /// Ownership boundary: the director owns the marker GameObject; this
    /// presenter owns ONLY its presentation child. Gameplay never references
    /// this assembly or Unity Audio - same decoupling as the PBSky renderer.
    /// </summary>
    public sealed class AmbientAudioPresenter : MonoBehaviour
    {
        private const string PresentationName = "BirdAudioPresentation";

        [Header("Sources")]
        [SerializeField] private AmbientEventDirector eventDirector;
        [SerializeField] private BirdAudioLibrary birdLibrary;

        [SerializeField]
        [Tooltip("0 = time-seeded. Any other value makes clip/pitch/volume picks deterministic (tests).")]
        private int randomSeed;

        /// <summary>Pure runtime record: the presenter's owned child and its source.</summary>
        private sealed class BirdPresentation
        {
            public GameObject PresentationObject;
            public AudioSource Source;
        }

        private readonly Dictionary<AmbientEventInstance, BirdPresentation> presentations =
            new Dictionary<AmbientEventInstance, BirdPresentation>();

        private System.Random random;

        /// <summary>Live bird audio presentations (tests and inspection).</summary>
        public int ActiveSourceCount => presentations.Count;

        private void OnEnable()
        {
            random = new System.Random(randomSeed != 0 ? randomSeed : Environment.TickCount);

            if (eventDirector == null)
            {
                return;
            }

            eventDirector.OnEventSpawned += HandleSpawned;
            eventDirector.OnEventReleased += HandleReleased;

            // Give a voice to birds already alive (re-enable): only the
            // missing presentations are created, never a second one.
            foreach (var instance in eventDirector.ActiveInstances)
            {
                HandleSpawned(instance);
            }
        }

        private void OnDisable()
        {
            if (eventDirector != null)
            {
                eventDirector.OnEventSpawned -= HandleSpawned;
                eventDirector.OnEventReleased -= HandleReleased;
            }

            // Destroy owned presentation children only - the director's
            // markers stay untouched.
            foreach (var presentation in presentations.Values)
            {
                if (presentation.Source != null)
                {
                    presentation.Source.Stop();
                }

                if (presentation.PresentationObject != null)
                {
                    Destroy(presentation.PresentationObject);
                }
            }

            presentations.Clear();
        }

        private void HandleSpawned(AmbientEventInstance instance)
        {
            if (instance == null || instance.Profile == null ||
                instance.Profile.Category != AmbientEventCategory.Birds)
            {
                return;
            }

            if (presentations.ContainsKey(instance) || instance.MarkerTransform == null)
            {
                return;
            }

            if (birdLibrary == null ||
                !BirdAudioSelection.TryPickClip(birdLibrary.Clips, random, out AudioClip clip))
            {
                // No valid clip available: no AudioSource is ever created.
                return;
            }

            var presentationObject = new GameObject(PresentationName);
            presentationObject.transform.SetParent(instance.MarkerTransform, false);

            var source = presentationObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = false;
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.spatialBlend = birdLibrary.SpatialBlend;
            source.rolloffMode = birdLibrary.Rolloff;
            source.minDistance = birdLibrary.MinDistance;
            source.maxDistance = birdLibrary.MaxDistance;
            source.priority = birdLibrary.Priority;
            source.pitch = BirdAudioSelection.PickInRange(birdLibrary.PitchRange, random);
            source.volume = BirdAudioSelection.PickInRange(birdLibrary.VolumeRange, random);
            source.Play();

            presentations.Add(instance, new BirdPresentation
            {
                PresentationObject = presentationObject,
                Source = source
            });
        }

        private void HandleReleased(AmbientEventInstance instance)
        {
            if (instance == null || !presentations.TryGetValue(instance, out var presentation))
            {
                return;
            }

            if (presentation.Source != null)
            {
                presentation.Source.Stop();
            }

            if (presentation.PresentationObject != null)
            {
                Destroy(presentation.PresentationObject);
            }

            presentations.Remove(instance);
        }
    }
}
