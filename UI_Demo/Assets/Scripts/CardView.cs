using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// Attach to the card prefab if you want a single place to tweak visuals/animations.
public class CardView : MonoBehaviour
{
    public Image frame;            // assign in prefab (or leave null to auto-find)
    public TextMeshProUGUI label;  // optional
    public CanvasGroup cg;         // optional (for fade-in)

    void Awake()
    {
        if (frame == null) frame = GetComponentInChildren<Image>(true);
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (cg == null) cg = GetComponent<CanvasGroup>();
    }

    public void Apply(string rarityLower)
    {
        string r = (rarityLower ?? "common").ToLowerInvariant();
        if (frame != null) frame.color = RarityColor(r);
        if (label != null) label.text = r.ToUpper();

        // simple reveal (optional)
        if (cg != null) { cg.alpha = 1f; } // replace with tween later if you like
    }

    Color RarityColor(string rarity)
    {
        switch (rarity)
        {
            case "common":    return new Color32(150,150,150,255);
            case "uncommon":  return new Color32(46,204,113,255);
            case "rare":      return new Color32(0,112,221,255);
            case "epic":      return new Color32(163,53,238,255);
            case "legendary": return new Color32(255,204,0,255);
            default:          return Color.white;
        }
    }
}
