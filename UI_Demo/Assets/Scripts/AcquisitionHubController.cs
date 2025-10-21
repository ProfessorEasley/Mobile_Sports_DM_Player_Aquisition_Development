using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controls navigation between Hub, Market, and Pack Opening panels.
/// Handles basic currency display and default pack setup.
/// Phase 1-ready: includes hooks for future emotion/telemetry resets.
/// </summary>
public class AcquisitionHubController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text coinsText;

    [Header("Buttons")]
    public Button goToMarketButton;
    public Button openPackButton;

    [Header("Panels")]
    public GameObject hubPanel;
    public GameObject marketPanel;
    public GameObject packPanel;

    [Header("Defaults")]
    [Tooltip("Default pack key used when opening directly from hub.")]
    public string defaultPackKey = "basic_pack";

    void Start()
    {
        // --- Validate references ---
        if (coinsText == null)
            Debug.LogWarning("[Hub] coinsText not assigned in Inspector.");
        if (goToMarketButton == null || openPackButton == null)
            Debug.LogWarning("[Hub] Buttons not assigned in Inspector.");
        if (hubPanel == null || marketPanel == null || packPanel == null)
            Debug.LogWarning("[Hub] Panels not assigned in Inspector.");

        // --- Display placeholder currency (could be replaced by PlayerWallet later) ---
        if (coinsText != null)
            coinsText.text = "Coins: 1200";

        // --- Navigation listeners ---
        if (goToMarketButton != null)
        {
            goToMarketButton.onClick.AddListener(() =>
            {
                hubPanel?.SetActive(false);
                marketPanel?.SetActive(true);
            });
        }

        if (openPackButton != null)
        {
            openPackButton.onClick.AddListener(() =>
            {
                hubPanel?.SetActive(false);
                packPanel?.SetActive(true);

                // Optional: reset emotional state at session start (if needed)
                EmotionalStateManager.Instance?.ResetSession();

                // Configure PackOpeningController
                var opener = packPanel?.GetComponentInChildren<PackOpeningController>(true);
                if (opener != null)
                {
                    opener.packType = defaultPackKey;
                }
                else
                {
                    Debug.LogWarning("[Hub] No PackOpeningController found under packPanel.");
                }
            });
        }
    }
}
