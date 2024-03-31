using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UniGLTF.Extensions.VRMC_vrm;
using UnityEngine;
using UniVRM10;

namespace DifficultClimbingVRM
{
    internal static class Settings
    {
#nullable enable
        public static ConfigEntry<string>? VRMPath { get; private set; }
        public static ConfigEntry<string?>? CurrentCharacter { get; private set; }

        public static List<CustomPlayerModel> PlayerModels { get; } = new List<CustomPlayerModel>();

        public static CustomPlayerModel? CurrentPlayerModel { get; private set; }

        public static bool ModelIsLoaded { get; private set; } = false;

        public static void Initialize(ConfigFile config)
        {
            VRMPath = config.Bind("General", nameof(VRMPath), "VRM", "The directory the VRM models will get loaded from");
            CurrentCharacter = config.Bind<string?>("General", nameof(CurrentCharacter), null, "The path to the currently loaded player model");

            // People need to be able to put their models somewhere
            if (!Directory.Exists(VRMPath.Value))
            {
                Directory.CreateDirectory(VRMPath.Value);
            }

            LoadPlayerModels();
        }

        public static void LoadPlayerModels()
        {
            ModelIsLoaded = false;
            PlayerModels.Clear();

            if (VRMPath == null || CurrentCharacter == null)
                return;

            string currentCharacterPath = Path.Combine(VRMPath.Value, CurrentCharacter.Value);

            foreach (string path in Directory.EnumerateFiles(VRMPath.Value, "*.vrm", SearchOption.AllDirectories))
            {
                try
                {
                    CustomPlayerModel playerModel = new CustomPlayerModel(path);
                    PlayerModels.Add(playerModel);

                    if (string.Equals(path, currentCharacterPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        CurrentPlayerModel = playerModel;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load player model {path}: {ex.Message}");
                }
            }

            if (CurrentPlayerModel == null && PlayerModels.Count > 0)
                SetActiveModel(PlayerModels[0]);

            ModelIsLoaded = true;
        }

        public static void SetActiveModel(CustomPlayerModel model)
        {
            CurrentPlayerModel = model;

            if (CurrentCharacter != null)
            {
                if (VRMPath != null && model != null)
                    CurrentCharacter.Value = Path.GetRelativePath(VRMPath.Value, model.FilePath);
                else
                    CurrentCharacter.Value = null;
            }
        }
    }

    internal class CustomPlayerModel
    {
        // Properties
        public string FilePath { get; }
        public ConfigEntry<string> Name { get; }
        public Metadata? Metadata {
            get
            {
                if (!hasMeta)
                {
                    try
                    {
                        meta = GetMetadata(FilePath).Result;
                    }
                    catch (FileLoadException ex) { Debug.LogError(string.Format("Failed to load data for {0}", ex.FileName)); }
                    hasMeta = true;
                }

                return meta;
            }
        }
        private Metadata? meta;
        private bool hasMeta;

        // Fixes
        public ConfigEntry<bool> OffsetEntireModel { get; }

        // Ajustments
        public ConfigEntry<float> Scale { get; }

        // Material Settings
        public ConfigEntry<float> MaterialBrightness { get; }

        // Crown
        public ConfigEntry<bool> AllowCrown { get; }
        public ConfigEntry<Vector3> CrownPosition { get; }

        public CustomPlayerModel(string path)
        {
            if (Settings.VRMPath == null)
                throw new Exception("Tried getting settings before initialization");

            if (!File.Exists(path))
                throw new FileNotFoundException();

            FilePath = path;

            ConfigFile modelSettings = new ConfigFile(Path.Combine(Path.ChangeExtension(FilePath, "cfg")), true);

            Name = modelSettings.Bind("Info", nameof(Name), Path.GetFileNameWithoutExtension(path), "The name displayed in-game");
            OffsetEntireModel = modelSettings.Bind("Customization.Fixes", nameof(OffsetEntireModel), false, "Moves the whole player forward to prevent weird arm bends (will make shadows not match correctly)");
            Scale = modelSettings.Bind("Customization.Adjustments", nameof(Scale), 1f, "The model scale multiplier");
            
            MaterialBrightness = modelSettings.Bind("Customization.Materials", nameof(MaterialBrightness), 0.8f, "The brightness that all materials will be multiplied by");

            AllowCrown = modelSettings.Bind("Customization.Adjustments.Crown", nameof(AllowCrown), true, "Is the game allowed to turn on the crown?");
            CrownPosition = modelSettings.Bind("Customization.Adjustments.Crown", nameof(CrownPosition), new Vector3(0, 1.462f, -0.065f), "The position the crown is set to");
        }

        public Task<Vrm10Instance> Load()
        {
            return Vrm10.LoadPathAsync(FilePath, controlRigGenerationOption: ControlRigGenerationOption.None);
        }

        private async Task<Metadata?> GetMetadata(string filePath)
        {
            byte[] file = File.ReadAllBytes(filePath);
            var gltfData = new UniGLTF.GlbLowLevelParser(filePath, file).Parse();

            Vrm10Data vrm10Data = Vrm10Data.Parse(gltfData); // ?? throw new FileLoadException("Could not load VRM data " + filePath, filePath);

            if (vrm10Data == null)
                return null;

            using var loader = new Vrm10Importer(vrm10Data);
            var thumbnail = await loader.LoadVrmThumbnailAsync();
            return new Metadata(vrm10Data.VrmExtension.Meta, thumbnail);
        }

        /*
        private Meta? GetMetadata(string filePath)
        {
            byte[] file = File.ReadAllBytes(filePath);
            var gltfData = new UniGLTF.GlbLowLevelParser(filePath, file).Parse();

            return GetMetadata(gltfData.GLTF.extensions);
        }


        private Meta? GetMetadata(UniGLTF.glTFExtension src)
        {
            if (src is UniGLTF.glTFExtensionImport extensions)
            {
                foreach (var kv in extensions.ObjectItems())
                {
                    if (kv.Key.GetUtf8String() == GltfDeserializer.ExtensionNameUtf8)
                    {
                        foreach (var kv2 in kv.Value.ObjectItems())
                        {
                            var key = kv2.Key.GetString();

                            if (key == "meta")
                            {
                                return GltfDeserializer.Deserialize_Meta(kv2.Value);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private async Task<Texture2D> LoadVrmThumbnailAsync(UniGLTF.GltfData data)
        {
            IAwaitCaller awaitCaller = new ImmediateCaller();

            if (!UniGLTF.Extensions.VRMC_vrm.GltfDeserializer.TryGet(data.GLTF.extensions, out var vrm))
            {
                return null;
            }

            if (Vrm10TextureDescriptorGenerator.TryGetMetaThumbnailTextureImportParam(data, vrm, out (SubAssetKey, VRMShaders.TextureDescriptor Param) kv))
            {
                var texture = await TextureFactory.GetTextureAsync(kv.Param, awaitCaller);
                return texture as Texture2D;
            }
            else
            {
                return null;
            }
        }
        */
    }

    public class Metadata
    {
        public Meta Meta { get; }
        public Texture2D Thumbnail { get; }

        public Metadata(Meta meta, Texture2D thumbnail)
        {
            Meta = meta;
            Thumbnail = thumbnail;
        }
    }
#nullable disable
}
