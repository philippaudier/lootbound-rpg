namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// Plain, Unity-free authoring parameters for the HeightField. A Provider
    /// maps the Unity ScriptableObject config into this at composition time, so
    /// the field itself never touches Unity.
    /// </summary>
    public sealed class HeightFieldSettings
    {
        // Kept for the domain-warp magnitude, to preserve exact legacy values.
        // Making the warp fully world-size-independent is a later, value-changing
        // decision - out of scope for the T2 refactor (zero visual change).
        public float WorldSize;

        public float MacroScale;
        public int MacroOctaves;
        public float MacroPersistence;
        public float MacroLacunarity;

        public float RidgeScale;
        public float RidgeStrength;

        public float ValleyScale;
        public float ValleyStrength;

        public float DetailScale;
        public float DetailStrength;

        public float GlobalHeightStrength;
    }
}
