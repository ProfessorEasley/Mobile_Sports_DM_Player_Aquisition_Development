using UnityEngine;
using TMPro;

public class EmotionDisplayUI : MonoBehaviour
{
    [Header("Text References")]
    public TextMeshProUGUI frustrationText;
    public TextMeshProUGUI satisfactionText;

    [Header("Animation Settings")]
    [Range(0.1f, 5f)] public float lerpSpeed = 2.5f;

    // Internal smoothed values
    private float _displayFr;
    private float _displaySa;

    void Start()
    {
        // Initialize displayed values with current snapshot
        if (EmotionalStateManager.Instance != null)
        {
            var snap = EmotionalStateManager.Instance.Snapshot();
            _displayFr = snap.fr;
            _displaySa = snap.sa;
        }
    }

    void Update()
    {
        if (EmotionalStateManager.Instance == null) return;

        var (fr, sa) = EmotionalStateManager.Instance.Snapshot();

        // Smoothly interpolate toward current emotion state
        _displayFr = Mathf.Lerp(_displayFr, fr, Time.deltaTime * lerpSpeed);
        _displaySa = Mathf.Lerp(_displaySa, sa, Time.deltaTime * lerpSpeed);

        // Update UI Texts
        if (frustrationText)
        {
            frustrationText.text = $"Frustration: {_displayFr:F1}";
            frustrationText.color = Color.Lerp(Color.white, Color.red, _displayFr / 10f);
        }

        if (satisfactionText)
        {
            satisfactionText.text = $"Satisfaction: {_displaySa:F1}";
            satisfactionText.color = Color.Lerp(Color.white, Color.green, _displaySa / 10f);
        }
    }
}
