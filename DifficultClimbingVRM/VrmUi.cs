using DifficultClimbingVRM.Patches;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace DifficultClimbingVRM
{
    internal class VrmUi : MonoBehaviour
    {
        private UIDocument ui;

        private bool isOpen;
        private bool hasGeneratedButtons;

        readonly List<TemplateContainer> buttons = new List<TemplateContainer>();

        /// <summary>
        /// Icon used when thumbnail can't be loaded
        /// </summary>
        public Texture2D NoIcon
        {
            get
            {
                // Load texture from resources if not loaded yet
                if (noIcon == null)
                {
                    noIcon = new Texture2D(2, 2); // Create a dummy Texture2D
                    noIcon.LoadImage(Properties.Resources.noicon); // Replace it with our default icon
                }
                return noIcon;
            }
        }
        private Texture2D noIcon;

        private void ChangeVrmPath()
        {
            // Not yet implemented as I don't have to time to find a Unity compatable way of opening folder dialogues.
        }

        private void OpenVrmPath()
        {
            // I don't think any other platform than Windows has 'explorer.exe' but I could be wrong.
            if (Application.platform == RuntimePlatform.WindowsPlayer && Directory.Exists(Settings.VRMPath.Value))
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    Arguments = Settings.VRMPath.Value,
                    FileName = "explorer.exe"
                };

                System.Diagnostics.Process.Start(startInfo);
            }
        }

        private void ReloadModels()
        {
            Settings.LoadPlayerModels();
            GenerateButtons();
        }

        private void Start()
        {
            AssetBundle bundle = AssetBundle.LoadFromMemory(Properties.Resources.vrmui);
            GameObject uiObject = Instantiate(bundle.LoadAsset<GameObject>("VRM UI"));
            DontDestroyOnLoad(uiObject);

            ui = uiObject.GetComponent<UIDocument>();
            ui.rootVisualElement.style.display = DisplayStyle.None;
            VisualElement mainContainer = ui.rootVisualElement[0];

            TemplateContainer characterButtonTemplate = mainContainer.Q<TemplateContainer>("CharacterButton");

            buttonContainer = characterButtonTemplate.parent;
            buttonTemplate = characterButtonTemplate.templateSource;

            Button editButton = mainContainer.Q<Button>("edit");
            Button openButton = mainContainer.Q<Button>("open");
            Button refreshButton = mainContainer.Q<Button>("refresh");

            editButton.RegisterCallback<MouseUpEvent>((evt) => ChangeVrmPath());
            openButton.RegisterCallback<MouseUpEvent>((evt) => OpenVrmPath());
            refreshButton.RegisterCallback<MouseUpEvent>((evt) => ReloadModels());

            // I have not yet implemented the edit folder button
            editButton.style.display = DisplayStyle.None;

            characterButtonTemplate.style.display = DisplayStyle.None;

            PauseMenuPatches.PauseMenuOpened += PauseMenuOpened;
            PauseMenuPatches.PauseMenuClosed += PauseMenuOpened;

            CreateVrmButton();
        }
        
        /// <summary>
        /// Adds the 'VRM' button to the pause menu
        /// </summary>
        private void CreateVrmButton()
        {
            //Create button to open UI
            GameObject menuCanvas = GameObject.Find("PauseMenuCanvas");
            GameObject pauseButton = menuCanvas.transform.Find("PauseMenu/RESUME").gameObject;

            Vector3 newButtonPos = pauseButton.transform.position;
            newButtonPos.x = 1778.582f; // Hardcoded positions would never betray me right?

            const string vrmName = "VRM";
            GameObject vrmButton = Instantiate(pauseButton, newButtonPos, pauseButton.transform.rotation, pauseButton.transform.parent);
            vrmButton.name = vrmName;
            vrmButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = vrmName;

            UnityEngine.UI.Button buttonComponent = vrmButton.GetComponent<UnityEngine.UI.Button>();
            buttonComponent.onClick.AddListener(VrmOpenButtonClick);
        }

        /// <summary>
        /// Gets called when the user presses escape or the pause button
        /// </summary>
        public void PauseMenuOpened()
        {
            if (!isOpen)
                return;

            // Close our model selector
            CloseMenu();

            // Cancel opening the pause menu
            PauseMenuPatches.PauseMenu.ResumeGame();
        }

        /// <summary>
        /// Gets called whenever the VRM button in the pause menu is pressed
        /// </summary>
        public void VrmOpenButtonClick()
        {
            OpenMenu();
        }

        /// <summary>
        /// Toggles if the model selector is open
        /// </summary>
        public void ToggleMenu()
        {
            if (isOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        private bool wasPaused = false;
        /// <summary>
        /// Opens the model selector
        /// </summary>
        public void OpenMenu()
        {
            isOpen = true;

            wasPaused = PauseMenu.GameIsPaused;
            PauseMenu.GameIsPaused = true;

            ui.rootVisualElement.style.display = DisplayStyle.Flex;
            if (!hasGeneratedButtons)
            {
                GenerateButtons();
                hasGeneratedButtons = true;
            }
        }

        /// <summary>
        /// Closes the model selector
        /// </summary>
        public void CloseMenu()
        {
            isOpen = false;
            PauseMenu.GameIsPaused = wasPaused;

            ui.rootVisualElement.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            // This code doesn't need to be ran if the menu isn't open
            if (!isOpen)
                return;

            // Makes sure the game is paused and the cursor is visible while selecting a model
            // Doesn't have to be executed every frame but another mod might break it otherwise
            Time.timeScale = 0;

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private VisualElement buttonContainer;
        private VisualTreeAsset buttonTemplate;

        /// <summary>
        /// Generates buttons for every model currently loaded
        /// </summary>
        private void GenerateButtons()
        {
            buttonContainer.Clear();

            GenerateButton(buttonContainer, buttonTemplate, null);

            foreach (CustomPlayerModel playerModel in Settings.PlayerModels)
            {
                GenerateButton(buttonContainer, buttonTemplate, playerModel);
            }
        }

        /// <summary>
        /// Generates a character selection model for a given player model
        /// </summary>
        /// <param name="buttonContainer">The container that contains all chracter buttons</param>
        /// <param name="buttonTemplate">The template used for creating a new button</param>
        /// <param name="model">The target model</param>
        private void GenerateButton(VisualElement buttonContainer, VisualTreeAsset buttonTemplate, CustomPlayerModel model)
        {
            TemplateContainer templateContainer = buttonTemplate.Instantiate();

            Button button = templateContainer.Q<Button>("character-button");
            Label label = templateContainer.Q<Label>("character-name");

            if (model != null)
            {
                // Create a button for specified VRM file
                templateContainer.name = "CharacterButton";

                label.text = model.Name.Value;

                if (model.Metadata != null)
                {
                    Texture2D thumbnail = model.Metadata.Thumbnail ?? NoIcon;
                    button.style.backgroundImage = new StyleBackground(thumbnail);
                }
                else
                {
                    button.style.backgroundImage = NoIcon;
                }
            }
            else
            {
                // Button for no model, would probably be better to create an instance of CustomPlayerModel for this

                templateContainer.name = "DefaultButton";
                label.text = "None";

                Texture2D defaultCharacterIcon = new Texture2D(2, 2); // Create a dummy Texture2D
                defaultCharacterIcon.LoadImage(Properties.Resources._default); // Replace it with our default character icon
                button.style.backgroundImage = defaultCharacterIcon;
            }

            // Register button press
            button.RegisterCallback<MouseUpEvent>((evt) => SetActiveButton(buttons, templateContainer, model));
            
            // Highlight button it's the current selected player model
            if (model == Settings.CurrentPlayerModel)
                templateContainer.AddToClassList("active");

            // Add it to the rest of the buttons
            buttonContainer.Add(templateContainer);

            // Keep track of it for later updating
            buttons.Add(templateContainer);
        }

        /// <summary>
        /// Changes which button is currently set to active and swaps the model accordingly
        /// </summary>
        /// <param name="modelContainer">List of all buttons</param>
        /// <param name="target">The button to be activated</param>
        /// <param name="targetModel">The model to be enabled</param>
        void SetActiveButton(List<TemplateContainer> modelContainer, VisualElement target, CustomPlayerModel targetModel)
        {
            // Make all buttons inactive
            foreach (VisualElement modelButton in modelContainer)
            {
                modelButton.RemoveFromClassList("active");
            }

            // Set the target model
            Settings.SetActiveModel(targetModel);
            StartCoroutine(DifficultClimbingReplacer.Instance.ReplacePlayerModel());

            // Make current button active
            target.AddToClassList("active");
        }
    }
}
