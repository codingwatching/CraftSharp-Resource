using System.Collections.Generic;
using System.Linq;

namespace CraftSharp.Resource.BedrockEntity
{
    public class EntityRenderDefinition
    {
        private static readonly char SP = System.IO.Path.DirectorySeparatorChar;
        
        // File versions
        public BedrockVersion FormatVersion;
        public BedrockVersion MinEngineVersion;
        // Identifier of this entity type
        public ResourceLocation EntityType;
        // Texture name => texture path in pack
        public readonly Dictionary<string, string> TexturePaths;
        // Material name => material identifier
        public readonly Dictionary<string, string> MaterialIdentifiers;
        // Variant name => geometry name
        public readonly Dictionary<string, string> GeometryNames;
        // State name => animation name
        public readonly Dictionary<string, string> AnimationNames;

        internal EntityRenderDefinition(BedrockVersion formatVersion, BedrockVersion minEnVersion, ResourceLocation entityType,
                Dictionary<string, string> texturePaths, Dictionary<string, string> matIds,
                Dictionary<string, string> geometryNames,Dictionary<string, string> animationNames)
        {
            FormatVersion = formatVersion;
            MinEngineVersion = minEnVersion;

            EntityType = entityType;

            TexturePaths = texturePaths;
            MaterialIdentifiers = matIds;
            GeometryNames = geometryNames;
            AnimationNames = animationNames;
        }

        public static EntityRenderDefinition FromJson(string resourceRoot, Json.JSONData data)
        {
            var defVersion = BedrockVersion.FromString(data.Properties["format_version"].StringValue);

            var desc = data.Properties["minecraft:client_entity"].Properties["description"];
            var entityType = ResourceLocation.FromString(desc.Properties["identifier"].StringValue);

            Dictionary<string, string> matIds;
            if (desc.Properties.ContainsKey("materials"))
            {
                matIds = desc.Properties["materials"].Properties.ToDictionary(x => x.Key,
                        x => x.Value.StringValue);
            }
            else
            {
                matIds = new();
            }

            Dictionary<string, string> texturePaths;
            if (desc.Properties.ContainsKey("textures"))
            {
                texturePaths = desc.Properties["textures"].Properties.ToDictionary(x => x.Key,
                        x => $"{resourceRoot}{SP}{x.Value.StringValue}");
            }
            else
            {
                texturePaths = new();
            }

            Dictionary<string, string> geometryNames;
            if (desc.Properties.ContainsKey("geometry"))
            {
                geometryNames = desc.Properties["geometry"].Properties.ToDictionary(x => x.Key, x => x.Value.StringValue);
            }
            else
            {
                geometryNames = new();
            }

            Dictionary<string, string> animationNames;
            if (desc.Properties.ContainsKey("animations"))
            {
                animationNames = desc.Properties["animations"].Properties.ToDictionary(x => x.Key, x => x.Value.StringValue);
            }
            else
            {
                animationNames = new();
            }

            var minEnVersion = BedrockEntityResourceManager.UNSPECIFIED_VERSION;
            if (desc.Properties.ContainsKey("min_engine_version"))
            {
                minEnVersion = BedrockVersion.FromString(desc.Properties["min_engine_version"].StringValue);
            }

            return new(defVersion, minEnVersion, entityType, texturePaths, matIds, geometryNames, animationNames);
        }
    }
}