using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Central controller for navigation between all panels.
/// Ensures only one main panel (Hub, Market, PackOpening, History) is visible at a time.
/// </summary>
public class AcquisitionHubController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text coinsText;

    [Header("Buttons")]
    public Button goToMarketButton;
    public Button myPacksButton; // Previously openPackButton

    [Header("Panels")]
    public GameObject hubPanel;
    public GameObject marketPanel;
    public GameObject packPanel;
    public GameObject dropHistoryPanel;
    public GameObject myPacksPanel; // Placeholder for later

    void Start()
    {
        if (coinsText != null)
            coinsText.text = "Coins: 1200";

        if (goToMarketButton != null)
            goToMarketButton.onClick.AddListener(ShowMarket);

        // Disable My Packs for now
        if (myPacksButton != null)
        {
            myPacksButton.interactable = false;
            myPacksButton.onClick.AddListener(ShowMyPacks); // Won’t do anything yet
        }

        ShowHub();
    }

    // --- Navigation Helpers ---
    public void ShowHub() => SetActivePanel(hubPanel);
    public void ShowMarket() => SetActivePanel(marketPanel);
    public void ShowMyPacks() => SetActivePanel(myPacksPanel);

    public void ShowPackOpening(string packKey)
    {
        SetActivePanel(packPanel);

        var opener = packPanel?.GetComponentInChildren<PackOpeningController>(true);
        if (opener != null)
            opener.OpenPackOfType(packKey);
    }

    public void ShowHistory()
    {
        SetActivePanel(dropHistoryPanel);
    }

    private void SetActivePanel(GameObject active)
    {
        hubPanel?.SetActive(active == hubPanel);
        marketPanel?.SetActive(active == marketPanel);
        packPanel?.SetActive(active == packPanel);
        dropHistoryPanel?.SetActive(active == dropHistoryPanel);
        if (myPacksPanel != null)
            myPacksPanel.SetActive(active == myPacksPanel);
    }
}
