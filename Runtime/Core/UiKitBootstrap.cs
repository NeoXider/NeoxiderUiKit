using UnityEngine;

namespace Neo.UIKit
{
    /// <summary>
    /// Single entry point on the UI root object: initializes the kit in Awake, registers all
    /// pages (including inactive ones), shows the start page before the first frame and hosts
    /// the dedicated 2D AudioSource for UI click sounds. Optionally handles Back/Escape.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UiKitBootstrap : MonoBehaviour
    {
        [SerializeField] private UiKitConfig config;
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private bool showStartPage = true;
        [Tooltip("Overrides the config's start page when non-empty.")]
        [SerializeField] private string startPageOverride = "";
        [SerializeField] private bool handleBackButton = true;

        /// <summary>The configuration asset used to initialize the kit.</summary>
        public UiKitConfig Config => config;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[UiKit] UiKitBootstrap has no UiKitConfig assigned.", this);
                return;
            }

            UiKit.Initialize(config);
            SetupAudioSource();
            RegisterPages();

            if (showStartPage)
            {
                string startId = ResolveStartPageId();
                if (!string.IsNullOrEmpty(startId))
                    UiKit.Pages.Show(startId);
            }
        }

        private void SetupAudioSource()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.GetComponent<AudioSource>();
                if (uiAudioSource == null)
                    uiAudioSource = gameObject.AddComponent<AudioSource>();
            }

            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
            UiKit.Audio.AttachClickSource(uiAudioSource);
        }

        private void RegisterPages()
        {
            UiPageBase[] pages = FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < pages.Length; i++)
                UiKit.Pages.Register(pages[i]);
        }

        private string ResolveStartPageId()
        {
            if (!string.IsNullOrEmpty(startPageOverride))
                return startPageOverride;

            for (int i = 0; i < config.pages.Count; i++)
            {
                if (config.pages[i] != null && config.pages[i].isStart)
                    return config.pages[i].pageId;
            }

            if (config.pages.Count > 0 && config.pages[0] != null)
                return config.pages[0].pageId;

            Debug.LogWarning("[UiKit] No start page configured.", this);
            return null;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (handleBackButton && Input.GetKeyDown(KeyCode.Escape))
                UiKit.Pages.Back();
        }
#endif
    }
}
