using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles visual presentation of a single card slot in the Pack Opening screen.
/// Supports rarity-based color mapping and simple fade-in reveals.
/// Phase 1–ready: can be extended to react to emotional state or hooks.
/// </summary>
public class CardView : MonoBehaviour
{
    [Header("References")]
    public Image frame;            // Assign in prefab (auto-finds if null)
    public TextMeshProUGUI label;  // Optional text label
    public CanvasGroup cg;         // Optional (for fade-in)

    [Header("Animation Settings")]
    [Range(0f, 1f)] public float revealAlpha = 1f;
    [Range(0.1f, 2f)] public float revealSpeed = 1f;

    void Awake()
    {
        if (frame == null) frame = GetComponentInChildren<Image>(true);
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (cg == null) cg = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Applies the correct color and label for a card's rarity.
    /// </summary>
    public void Apply(string rarityLower)
    {
        string rarity = (rarityLower ?? "common").ToLowerInvariant();

        // Defensive: make sure we have frame or label
        if (frame == null && label == null)
        {
            Debug.LogWarning($"[CardView] No UI components assigned on {gameObject.name}");
            return;
        }

        // Apply color and label
        if (frame != null)
            frame.color = GetRarityColor(rarity);

        if (label != null)
            label.text = rarity.ToUpperInvariant();

        // Simple reveal fade (optional)
        if (cg != null)
        {
            cg.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }
    }

    System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * revealSpeed;
            if (cg != null)
                cg.alpha = Mathf.Lerp(0f, revealAlpha, t);
            yield return null;
        }
    }

    /// <summary>
    /// Returns a color for each rarity. Can later be sourced from DropConfigManager.
    /// </summary>
    Color GetRarityColor(string rarity)
    {
        switch (rarity)
        {
            case "common":    return new Color32(150, 150, 150, 255);
            case "uncommon":  return new Color32(46, 204, 113, 255);
            case "rare":      return new Color32(0, 112, 221, 255);
            case "epic":      return new Color32(163, 53, 238, 255);
            case "legendary": return new Color32(255, 204, 0, 255);
            default:          return Color.white;
        }
    }
}
