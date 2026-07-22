using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Chunking;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Reflection guards for the ownership contract frozen in
    /// WORLD_ENGINE_ARCHITECTURE.md section 6.1: the generator never learns that
    /// chunks exist, and a chunk never learns who generates. If someone wires a
    /// forbidden dependency through a field, property or method signature, these
    /// go red immediately.
    /// </summary>
    public class ChunkOwnershipTests
    {
        private const string ChunkingNamespace = "Lootbound.Gameplay.World.Chunking";

        [Test]
        public void Generator_ReferencesNoChunkingType()
        {
            foreach (Type type in DeclaredMemberTypes(typeof(ProceduralTerrainGenerator)))
            {
                Assert.AreNotEqual(ChunkingNamespace, Namespace(type),
                    $"ProceduralTerrainGenerator must not reference {type.Name} - the generator never learns chunks exist");
            }
        }

        [Test]
        public void TerrainChunk_DoesNotKnowTheGenerator()
        {
            var forbidden = new HashSet<Type>
            {
                typeof(ProceduralTerrainGenerator),
                typeof(IWorldHeightSampler),
                typeof(IWorldSplatSampler),
            };

            foreach (Type type in DeclaredMemberTypes(typeof(TerrainChunk)))
            {
                Assert.IsFalse(forbidden.Contains(type),
                    $"TerrainChunk must not reference {type.Name} - it displays data and never generates");
            }
        }

        [Test]
        public void BuildStateAndData_DoNotKnowScheduler_StreamerOrPool()
        {
            var forbidden = new HashSet<Type>
            {
                typeof(TerrainChunkBuildScheduler),
                typeof(TerrainChunkStreamer),
                typeof(ChunkPool),
            };

            foreach (Type owner in new[] { typeof(TerrainChunkBuildState), typeof(TerrainChunkData), typeof(TerrainChunkBuilder) })
            {
                foreach (Type type in DeclaredMemberTypes(owner))
                {
                    Assert.IsFalse(forbidden.Contains(type),
                        $"{owner.Name} must not reference {type.Name} - dependencies only ever point from the scheduler toward the builder");
                }
            }
        }

        private static string Namespace(Type type)
        {
            Type inspected = type.IsArray ? type.GetElementType() : type;
            return inspected?.Namespace ?? string.Empty;
        }

        /// <summary>
        /// Every type appearing in the DECLARED surface of a type: field types,
        /// property types, method parameter and return types (public + private).
        /// </summary>
        private static IEnumerable<Type> DeclaredMemberTypes(Type owner)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;

            foreach (FieldInfo field in owner.GetFields(flags))
            {
                yield return field.FieldType;
            }
            foreach (PropertyInfo property in owner.GetProperties(flags))
            {
                yield return property.PropertyType;
            }
            foreach (MethodInfo method in owner.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }
        }
    }
}
