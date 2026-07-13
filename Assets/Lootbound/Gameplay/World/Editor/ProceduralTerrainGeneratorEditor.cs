#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Lootbound.Gameplay.World.Editor
{
    /// <summary>
    /// Custom editor for ProceduralTerrainGenerator.
    /// Provides generation controls in the Inspector.
    /// </summary>
    [CustomEditor(typeof(ProceduralTerrainGenerator))]
    public class ProceduralTerrainGeneratorEditor : UnityEditor.Editor
    {
        private int customSeed;
        private bool showAdvanced;

        public override void OnInspectorGUI()
        {
            ProceduralTerrainGenerator generator = (ProceduralTerrainGenerator)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generation Controls", EditorStyles.boldLabel);

            // Validate configuration
            if (generator.Config == null)
            {
                EditorGUILayout.HelpBox("Configuration is not assigned.", MessageType.Error);
                return;
            }

            if (!generator.Config.Validate(out string error))
            {
                EditorGUILayout.HelpBox($"Invalid configuration: {error}", MessageType.Error);
            }

            EditorGUILayout.BeginHorizontal();

            // Generate with default seed
            if (GUILayout.Button("Generate Default", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Generate Terrain");
                generator.GenerateDefault();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            // Generate with random seed
            if (GUILayout.Button("Generate Random", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Generate Terrain");
                generator.GenerateRandom();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            // Regenerate current
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Regenerate Current Seed", GUILayout.Height(25)))
            {
                Undo.RecordObject(generator, "Regenerate Terrain");
                generator.Regenerate();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Terrain", GUILayout.Height(25)))
            {
                Undo.RecordObject(generator, "Clear Terrain");
                generator.ClearTerrain();
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            // Custom seed generation
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            customSeed = EditorGUILayout.IntField("Custom Seed", customSeed);

            if (GUILayout.Button("Generate", GUILayout.Width(80)))
            {
                Undo.RecordObject(generator, "Generate Terrain");
                generator.Generate(customSeed);
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            // Generation status
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Is Generated", generator.IsGenerated);
            EditorGUILayout.IntField("Current Seed", generator.CurrentSeed);
            EditorGUI.EndDisabledGroup();

            // Context info if generated
            if (generator.IsGenerated && generator.Context != null)
            {
                var ctx = generator.Context;

                EditorGUILayout.Space(5);
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Generation Details", true);

                if (showAdvanced)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.LabelField("Heights", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Min: {ctx.MinHeight * ctx.TerrainHeight:0.0}m");
                    EditorGUILayout.LabelField($"Max: {ctx.MaxHeight * ctx.TerrainHeight:0.0}m");
                    EditorGUILayout.LabelField($"Average: {ctx.AverageHeight * ctx.TerrainHeight:0.0}m");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Spawn", EditorStyles.boldLabel);
                    EditorGUILayout.Vector3Field("Position", ctx.SpawnPosition);
                    EditorGUILayout.LabelField($"Slope: {ctx.SpawnSlope:0.1}°");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total: {ctx.TotalGenerationTimeMs}ms");
                    EditorGUILayout.LabelField($"Heightmap: {ctx.HeightmapGenerationTimeMs}ms");
                    EditorGUILayout.LabelField($"Application: {ctx.TerrainApplicationTimeMs}ms");
                    EditorGUILayout.LabelField($"Painting: {ctx.PaintingTimeMs}ms");

                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
#endif
