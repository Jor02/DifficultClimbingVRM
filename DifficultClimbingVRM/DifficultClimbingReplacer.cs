using BepInEx;
using System.IO;
using UnityEngine;
using UniVRM10;
using UniGLTF;
using HarmonyLib;
using System.Threading.Tasks;
using System.Collections;
using System.Linq;
using DifficultClimbingVRM.Patches;
using DifficultClimbingVRM.PoseSyncing;

namespace DifficultClimbingVRM;

[BepInPlugin(PluginInfo.PluginGuid, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class DifficultClimbingReplacer : BaseUnityPlugin
{
    private readonly HumanBodyBones[] armBones = [HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand];

    private string VrmPath;

    private CustomPlayerModel currentPlayerModel;
    private Vrm10Instance playerVRM;
    PoseSynchronizer poseSynchronizer;

    GameObject cloth;
    SkinnedMeshRenderer bodyMesh;

    private void OnPlayerSpawned(GameObject player)
    {
        Logger.LogInfo("Player spawned.");

        Transform climber = player.transform.Find("Climber_Hero_Body_Prefab");
        Animator climberAnimator = climber.GetComponent<Animator>();

        if (AssertAndDisable(climber == null, "Couldn't fetch player"))
            return;

        // Get the cloth
        cloth = climber!.transform.Find("HeroCharacter/BumCoverCloth").gameObject;

        Transform body = climber!.transform.Find("HeroCharacter/Body");
        if (AssertAndDisable(body == null, "Couldn't fetch body"))
            return;

        bodyMesh = body!.GetComponent<SkinnedMeshRenderer>();

        if (AssertAndDisable(bodyMesh == null, "Couldn't fetch body renderer"))
            return;

        // Some null reference checking (just in case other mods break something)
        if (playerVRM != null && poseSynchronizer == null)
        {
            // Something went wrong somewhere so we'll just reload the player.
            Destroy(playerVRM);
            playerVRM = null;
        }

        // Load player or reset climber reference
        if (playerVRM == null)
        {
            StartCoroutine(LoadPlayerModel(climberAnimator));
            return;
        }
        else
        {
            poseSynchronizer.OriginAnimator = climberAnimator;
        }

        // Stop rendering the main player
        HideClimber();
    }

    private void Awake()
    {
        // Plugin loaded notification
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        //Initialize configuration settings (and create player model directory)
        Settings.Initialize(Config);

        VrmPath = Settings.VRMPath.Value;
        if (AssertAndDisable(!Directory.Exists(VrmPath), "Could not create VRM folder"))
            return;

        PlayerSpawnerPatches.PlayerSpawned += OnPlayerSpawned;

        Harmony.CreateAndPatchAll(typeof(PlayerSpawnerPatches));
        Harmony.CreateAndPatchAll(typeof(IKControlPatches));
    }

    public IEnumerator LoadPlayerModel(Animator climber)
    {
        currentPlayerModel = Settings.CurrentPlayerModel;

        if (currentPlayerModel == null)
            yield break;

        Logger.LogInfo($"Loading {currentPlayerModel.FilePath}.");

        Task<Vrm10Instance> instanceTask = currentPlayerModel.Load();

        // Wait for the model loading task to complete (no idea how necessary a task is since the game still freezes).
        while (!instanceTask.IsCompleted)
        {
            yield return null; // Yield until the next frame.
        }

        if (AssertAndDisable(instanceTask.IsFaulted, $"Failed while trying to load {currentPlayerModel.FilePath}"))
            yield break;

        // VRM (probably) successfully loaded.
        Vrm10Instance instance = instanceTask.Result;
        if (AssertAndDisable(instance == null, $"Could not load {currentPlayerModel.FilePath}"))
            yield break;

        // Mostly just for easier finding in UnityExplorer.
        instance!.name = currentPlayerModel.Name.Value;

        // Default toon shaders don't work that well in-game.
        FixMaterials(instance);

        // Get height of the imported model.
        Vector3 p1 = new Vector3(0, instance.Humanoid.Head.position.y, 0);
        Vector3 p2 = new Vector3(0, instance.transform.position.y, 0);
        float height = Vector3.Distance(p1, p2);

        // Adjust height of imported model.
        const float OriginalPlayerHeight = 1.7f; // Height of the in-game climber.
        float playerScale = OriginalPlayerHeight / height; // Calculate the scale the player needs to be multiplied by.
        playerScale *= currentPlayerModel.Scale.Value; // Apply scale set in configuration
        instance.transform.localScale = Vector3.one * playerScale; // Apply new scale.

        // Prevent imported model from being destroyed if the scene were to be reloaded
        DontDestroyOnLoad(instance.gameObject);

        // Some references
        playerVRM = instance;
        BoneMap[] vrmBoneMap = instance.Humanoid.BoneMap.Select(x => (BoneMap)x).ToArray();

        // Add pose syncing
        poseSynchronizer = PoseSynchronizer.CreateComponent(playerVRM.gameObject, climber, instance.GetComponent<Animator>(), vrmBoneMap, armBones);
        poseSynchronizer.PrePoseCallback += PreparePose;

        // Stop rendering the main player
        HideClimber();
    }

    // Offsets either the hands/arms or the entire player to not clip into walls
    // Not the best solution but good enough for now (feel free to PR a better solution)
    private void PreparePose()
    {
        poseSynchronizer.BoneOffsets.Clear();

        if (currentPlayerModel.OffsetEntireModel.Value)
        {
            float smallestDistance = Mathf.Min(IKControlPatches.HandSurfaceDistanceL, IKControlPatches.HandSurfaceDistanceR);

            if (smallestDistance < 0)
                poseSynchronizer.BoneOffsets[HumanBodyBones.Hips] = Vector3.forward * smallestDistance;
        }
        else
        {
            poseSynchronizer.BoneOffsets[HumanBodyBones.LeftHand] = Vector3.forward * IKControlPatches.HandSurfaceDistanceL;
            poseSynchronizer.BoneOffsets[HumanBodyBones.RightHand] = Vector3.forward * IKControlPatches.HandSurfaceDistanceR;

            poseSynchronizer.BoneOffsets[HumanBodyBones.LeftLowerArm] = Vector3.forward * (IKControlPatches.HandSurfaceDistanceL / 2);
            poseSynchronizer.BoneOffsets[HumanBodyBones.RightLowerArm] = Vector3.forward * (IKControlPatches.HandSurfaceDistanceR / 2);
        }
    }

    /// <summary>
    /// Hides the original climber model
    /// </summary>
    private void HideClimber()
    {
        bodyMesh!.forceRenderingOff = true;
        bodyMesh.updateWhenOffscreen = true;
        cloth.SetActive(false);
    }

    /// <summary>
    /// Shows the original climber model again
    /// </summary>
    private void ShowClimber()
    {
        bodyMesh!.forceRenderingOff = false;
        bodyMesh.updateWhenOffscreen = false;
        cloth.SetActive(true);
    }

    /// <remarks>
    /// WIP Not Finished
    /// </remarks>
    private static void FixMaterials(Vrm10Instance instance)
    {
        const float brightness = .8f;
        foreach (var material in instance
        .GetComponentsInChildren<Renderer>()
        .SelectMany(renderer => renderer.materials)
        .Where(material => material.HasProperty("_Color")))
        {
            var color = material.GetColor("_Color");
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            material.SetColor("_Color", color);

            material.renderQueue = 1982;
        }
    }

    public bool AssertAndDisable(bool assertion, string error)
    {
        if (assertion)
        {
            Logger.LogError(error);
            enabled = false;
            return true;
        }
        return false;
    }
}