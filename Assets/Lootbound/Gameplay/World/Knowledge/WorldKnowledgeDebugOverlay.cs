using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.World.Coordinates;
using Lootbound.World.Processing;

namespace Lootbound.Gameplay.World.Knowledge
{
    /// <summary>
    /// Development overlay for World Knowledge. Toggle with F9 (F8 is taken by the
    /// expedition panel); keys 1-7 pick the field. It samples the selected derived
    /// field over the world extent into a small texture with simple colours - a
    /// dev tool to SEE what the engine understands, never a final render.
    /// </summary>
    public sealed class WorldKnowledgeDebugOverlay : MonoBehaviour
    {
        private enum Mode { Slope, Curvature, Roughness, Exposure, Hydrology, Traversability, Landscape, CostMountain, CostAnimal, Accessibility, Isolation, Connectivity }

        [SerializeField] private ProceduralTerrainGenerator generator;
        [SerializeField] private Key toggleKey = Key.F9;
        [SerializeField] private int textureResolution = 96;
        [SerializeField] private int panelSize = 260;

        private bool _visible;
        private Mode _mode = Mode.Landscape;
        private WorldKnowledge _knowledge;
        private Lootbound.World.Processing.TerrainCostField _mountainCost;
        private Lootbound.World.Processing.TerrainCostField _animalCost;
        private Lootbound.World.Processing.TerritorialIdentityField _territorial;
        private int _builtSeed = int.MinValue;
        private Texture2D _texture;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[toggleKey].wasPressedThisFrame)
            {
                _visible = !_visible;
                if (_visible) Rebuild();
            }

            // Territorial Intelligence direct toggles: the debug IS the proof.
            if (kb.f10Key.wasPressedThisFrame) ToggleTerritorial(Mode.Accessibility);
            else if (kb.f11Key.wasPressedThisFrame) ToggleTerritorial(Mode.Isolation);
            else if (kb.f12Key.wasPressedThisFrame) ToggleTerritorial(Mode.Connectivity);

            if (!_visible) return;

            Mode? pick = null;
            if (kb.digit1Key.wasPressedThisFrame) pick = Mode.Slope;
            else if (kb.digit2Key.wasPressedThisFrame) pick = Mode.Curvature;
            else if (kb.digit3Key.wasPressedThisFrame) pick = Mode.Roughness;
            else if (kb.digit4Key.wasPressedThisFrame) pick = Mode.Exposure;
            else if (kb.digit5Key.wasPressedThisFrame) pick = Mode.Hydrology;
            else if (kb.digit6Key.wasPressedThisFrame) pick = Mode.Traversability;
            else if (kb.digit7Key.wasPressedThisFrame) pick = Mode.Landscape;
            else if (kb.digit8Key.wasPressedThisFrame) pick = Mode.CostMountain;
            else if (kb.digit9Key.wasPressedThisFrame) pick = Mode.CostAnimal;

            if (pick.HasValue && pick.Value != _mode)
            {
                _mode = pick.Value;
                Redraw();
            }
        }

        private void ToggleTerritorial(Mode mode)
        {
            if (_visible && _mode == mode)
            {
                _visible = false;
                return;
            }
            _mode = mode;
            _visible = true;
            Rebuild();
        }

        private void Rebuild()
        {
            if (generator == null || generator.Config == null || !generator.IsGenerated) return;
            if (_knowledge == null || _builtSeed != generator.CurrentSeed)
            {
                // Knowledge over the FINAL relief (G4): the carved refuge and
                // landmark seats are now visible to every analyzer and cost.
                _knowledge = WorldKnowledgeComposer.Build(
                    generator.Config, generator.CurrentSeed, generator.Context.Bounds,
                    new Providers.SampledWorldHeightField(generator));
                _mountainCost = new Lootbound.World.Processing.TerrainCostField(
                    _knowledge.Slope, _knowledge.Cliff, _knowledge.Roughness, _knowledge.RiverMask,
                    _knowledge.Landscape, TraversalProfiles.Mountain());
                _animalCost = new Lootbound.World.Processing.TerrainCostField(
                    _knowledge.Slope, _knowledge.Cliff, _knowledge.Roughness, _knowledge.RiverMask,
                    _knowledge.Landscape, TraversalProfiles.Animal());
                // Territorial Intelligence reads the DEFAULT perception's cost
                // view - measures are perception-relative by construction.
                _territorial = new Lootbound.World.Processing.TerritorialIdentityField(
                    _knowledge.Traversability, new Lootbound.World.Processing.TerritorialSettings());
                _builtSeed = generator.CurrentSeed;
            }
            Redraw();
        }

        private void Redraw()
        {
            if (_knowledge == null || generator == null || generator.Config == null || generator.Context == null) return;

            int res = Mathf.Max(8, textureResolution);
            if (_texture == null || _texture.width != res)
            {
                _texture = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            }

            // Sample across the materialized region (Refuge-centred), not [0, WorldSize].
            WorldBounds bounds = generator.Context.Bounds;
            var pixels = new Color[res * res];
            for (int v = 0; v < res; v++)
            {
                double wz = bounds.MinZ + (v / (double)(res - 1)) * bounds.SizeZ;
                for (int u = 0; u < res; u++)
                {
                    double wx = bounds.MinX + (u / (double)(res - 1)) * bounds.SizeX;
                    pixels[v * res + u] = ColorAt(new WorldCoordinate(wx, wz));
                }
            }
            _texture.SetPixels(pixels);
            _texture.Apply();
        }

        private Color ColorAt(WorldCoordinate c)
        {
            switch (_mode)
            {
                case Mode.Slope:
                    return Color.Lerp(Color.green, Color.red, Mathf.Clamp01(_knowledge.Slope.Evaluate(c) / 60f));
                case Mode.Curvature:
                {
                    float k = _knowledge.Curvature.Evaluate(c) / 20f;
                    return k >= 0f
                        ? Color.Lerp(Color.gray, Color.red, Mathf.Clamp01(k))
                        : Color.Lerp(Color.gray, Color.blue, Mathf.Clamp01(-k));
                }
                case Mode.Roughness:
                    return Color.Lerp(Color.black, Color.white, Mathf.Clamp01(_knowledge.Roughness.Evaluate(c) / 10f));
                case Mode.Exposure:
                {
                    float a = _knowledge.Exposure.Evaluate(c);
                    if (a < 0f) return Color.gray;
                    return Color.HSVToRGB(a / 360f, 0.7f, 0.9f);
                }
                case Mode.Hydrology:
                    if (_knowledge.RiverMask.Evaluate(c)) return new Color(0.1f, 0.3f, 1f);
                    return Color.Lerp(new Color(0.5f, 0.4f, 0.25f), new Color(0.2f, 0.5f, 0.9f),
                        Mathf.Clamp01(_knowledge.WaterTable.Evaluate(c)));
                case Mode.Traversability:
                    return Color.Lerp(Color.green, Color.red, Mathf.Clamp01((_knowledge.Traversability.Evaluate(c) - 1f) / 30f));
                case Mode.CostMountain:
                    return Color.Lerp(Color.green, Color.red, Mathf.Clamp01((_mountainCost.Evaluate(c) - 1f) / 30f));
                case Mode.CostAnimal:
                    return Color.Lerp(Color.green, Color.red, Mathf.Clamp01((_animalCost.Evaluate(c) - 1f) / 30f));
                case Mode.Landscape:
                    return LandscapeColor(_knowledge.Landscape.Evaluate(c));
                case Mode.Accessibility:
                    return Color.Lerp(Color.black, Color.green, _territorial.Evaluate(c).Accessibility);
                case Mode.Isolation:
                    return Color.Lerp(Color.black, Color.red, _territorial.Evaluate(c).Isolation);
                case Mode.Connectivity:
                    return Color.Lerp(Color.black, Color.cyan, _territorial.Evaluate(c).Connectivity);
                default:
                    return Color.magenta;
            }
        }

        private static Color LandscapeColor(LandscapeType t)
        {
            switch (t)
            {
                case LandscapeType.Plain: return new Color(0.8f, 0.75f, 0.45f);
                case LandscapeType.Valley: return new Color(0.2f, 0.6f, 0.2f);
                case LandscapeType.Ridge: return new Color(0.9f, 0.55f, 0.2f);
                case LandscapeType.Mountain: return new Color(0.55f, 0.55f, 0.55f);
                case LandscapeType.Plateau: return new Color(0.75f, 0.65f, 0.4f);
                case LandscapeType.Pass: return new Color(0.3f, 0.8f, 0.8f);
                case LandscapeType.Basin: return new Color(0.25f, 0.35f, 0.7f);
                case LandscapeType.Cliff: return new Color(0.7f, 0.15f, 0.15f);
                default: return Color.magenta;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;

            GUILayout.BeginArea(new Rect(10, 10, panelSize + 20, panelSize + 70), GUI.skin.box);
            GUILayout.Label($"World Knowledge  [F9]   mode: {_mode}");
            GUILayout.Label("1 Slope  2 Curv  3 Rough  4 Aspect  5 Hydro  6 Cost  7 Landscape  8 Cost:Mtn  9 Cost:Animal");
            GUILayout.Label("F10 Accessibility  F11 Isolation  F12 Connectivity   (territorial - slow redraw)");

            if (_texture != null)
            {
                Rect r = GUILayoutUtility.GetRect(panelSize, panelSize);
                GUI.DrawTexture(r, _texture, ScaleMode.StretchToFill);
            }
            else
            {
                GUILayout.Label(generator != null && generator.IsGenerated ? "(building...)" : "(generate a world first)");
            }

            GUILayout.EndArea();
        }
    }
}
