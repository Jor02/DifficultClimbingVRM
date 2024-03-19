using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        }

        public static void SetActiveModel(CustomPlayerModel model)
        {
            CurrentPlayerModel = model;

            if (VRMPath != null && CurrentCharacter != null)
                CurrentCharacter.Value = Path.GetRelativePath(VRMPath.Value, model.FilePath);
        }
    }

    internal class CustomPlayerModel
    {
        // Properties
        public string FilePath { get; }
        public ConfigEntry<string> Name { get; }

        // Fixes
        public ConfigEntry<bool> OffsetEntireModel { get; }

        // Ajustments
        public ConfigEntry<float> Scale { get; }

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

            AllowCrown = modelSettings.Bind("Customization.Adjustments.Crown", nameof(AllowCrown), true, "Is the game allowed to turn on the crown?");
            CrownPosition = modelSettings.Bind("Customization.Adjustments.Crown", nameof(CrownPosition), new Vector3(0, 1.462f, -0.065f), "The position the crown is set to");
        }

        public Task<Vrm10Instance> Load()
        {
            return Vrm10.LoadPathAsync(FilePath, controlRigGenerationOption: ControlRigGenerationOption.None);
        }
    }
#nullable disable
}
