using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Providers;

namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// The FIRST concrete implementation of a World Field: elevation as a pure,
    /// deterministic function of a WorldCoordinate. There is no grid and no
    /// single-terrain assumption - the field can be evaluated anywhere, at any
    /// scale, forever. Noise and remap come from injected Providers, so this
    /// class holds no Unity dependency.
    ///
    /// The math mirrors the legacy TerrainNoiseCore.EvaluateHeight EXACTLY
    /// (Mathf.Pow == (float)Math.Pow, Mathf.Abs == MathF.Abs, Mathf.Clamp01,
    /// Mathf.PerlinNoise via the injected source), so the refactor produces
    /// bit-identical results - a golden test guards this.
    /// </summary>
    public sealed class HeightField : IWorldField<float>
    {
        private readonly INoiseSource _noise;
        private readonly IHeightRemap _remap;
        private readonly HeightFieldSettings _s;
        private readonly NoiseOffsets _o;

        public HeightField(INoiseSource noise, IHeightRemap remap, HeightFieldSettings settings, NoiseOffsets offsets)
        {
            _noise = noise ?? throw new ArgumentNullException(nameof(noise));
            _remap = remap ?? throw new ArgumentNullException(nameof(remap));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _o = offsets;
        }

        public float Evaluate(WorldCoordinate coordinate)
        {
            // Sampled in float, exactly like the legacy code, so a coordinate that
            // held a float value round-trips to the identical float.
            float worldX = (float)coordinate.X;
            float worldZ = (float)coordinate.Z;
            float worldSize = _s.WorldSize;

            // Light domain warping for more organic shapes.
            const float warpStrength = 0.15f;
            float warpScale = _s.MacroScale * 0.5f;
            float warpX = SamplePerlin(worldX + _o.WarpOffsetX, worldZ + _o.WarpOffsetZ, warpScale) * warpStrength * worldSize * 0.1f;
            float warpZ = SamplePerlin(worldX + _o.WarpOffsetX + 1000, worldZ + _o.WarpOffsetZ + 1000, warpScale) * warpStrength * worldSize * 0.1f;

            float warpedX = worldX + warpX;
            float warpedZ = worldZ + warpZ;

            // 1. Macro terrain - main shape.
            float macro = SampleFBM(
                warpedX + _o.MacroOffsetX,
                warpedZ + _o.MacroOffsetZ,
                _s.MacroScale, _s.MacroOctaves, _s.MacroPersistence, _s.MacroLacunarity);

            // 2. Valley features - low areas and corridors.
            float valley = 0f;
            if (_s.ValleyStrength > 0f)
            {
                float valleyNoise = SampleFBM(worldX + _o.ValleyOffsetX, worldZ + _o.ValleyOffsetZ, _s.ValleyScale, 3, 0.5f, 2f);
                float valleyMask = 1f - Clamp01(macro * 1.5f);
                valley = (1f - Abs(valleyNoise * 2f - 1f)) * valleyMask;
                valley = Pow(valley, 1.5f) * _s.ValleyStrength;
            }

            // 3. Ridge features - high points.
            float ridge = 0f;
            if (_s.RidgeStrength > 0f)
            {
                float ridgeNoise = SampleFBM(worldX + _o.RidgeOffsetX, worldZ + _o.RidgeOffsetZ, _s.RidgeScale, 3, 0.45f, 2.1f);
                ridge = 1f - Abs(ridgeNoise * 2f - 1f);
                ridge = Pow(ridge, 2f);
                float ridgeMask = Clamp01((macro - 0.4f) * 2f);
                ridge *= ridgeMask * _s.RidgeStrength;
            }

            // 4. Detail noise - fine variation.
            float detail = 0f;
            if (_s.DetailStrength > 0f)
            {
                detail = SampleFBM(worldX + _o.DetailOffsetX, worldZ + _o.DetailOffsetZ, _s.DetailScale, 2, 0.5f, 2f) * _s.DetailStrength;
            }

            float height = macro;
            height -= valley * 0.3f;
            height += ridge * 0.25f;
            height += detail;

            height = Clamp01(height);
            height = _remap.Evaluate(height);
            height *= _s.GlobalHeightStrength;

            return height;
        }

        private float SamplePerlin(float worldX, float worldZ, float scale)
        {
            return _noise.Sample(worldX / scale, worldZ / scale);
        }

        private float SampleFBM(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float x = worldX / scale * frequency;
                float z = worldZ / scale * frequency;
                value += _noise.Sample(x, z) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return value / maxValue;
        }

        // Exact mirrors of the legacy UnityEngine.Mathf semantics.
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Abs(float v) => MathF.Abs(v);
        private static float Pow(float b, float e) => (float)Math.Pow(b, e); // Mathf.Pow == (float)Math.Pow
    }
}
