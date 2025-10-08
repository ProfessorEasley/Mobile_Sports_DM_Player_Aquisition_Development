using UnityEngine;
using TMPro;

public class EmotionDisplayUI : MonoBehaviour
{
    [Header("Text References")]
    public TextMeshProUGUI frustrationText;
    public TextMeshProUGUI satisfactionText;
    public TextMeshProUGUI envyText;

    [Header("Animation Settings")]
    [Range(0.1f, 5f)] public float lerpSpeed = 2.5f;

    // Internal smoothed values
    private float _displayFr;
    private float _displaySa;
    private float _displayEv;

    void Start()
    {
        // Initialize displayed values with current snapshot
        if (EmotionalStateManager.Instance != null)
        {
            var snap = EmotionalStateManager.Instance.Snapshot();
            _displayFr = snap.fr;
            _displaySa = snap.sa;
            _displayEv = snap.ev;
        }
    }

    void Update()
    {
        if (EmotionalStateManager.Instance == null) return;

        var (fr, sa, ev) = EmotionalStateManager.Instance.Snapshot();

        // Smoothly interpolate toward current emotion state
        _displayFr = Mathf.Lerp(_displayFr, fr, Time.deltaTime * lerpSpeed);
        _displaySa = Mathf.Lerp(_displaySa, sa, Time.deltaTime * lerpSpeed);
        _displayEv = Mathf.Lerp(_displayEv, ev, Time.deltaTime * lerpSpeed);

        // Optional: color intensity based on value
        if (frustrationText) {
            frustrationText.text = $"Frustration: {_displayFr:F1}";
            frustrationText.color = Color.Lerp(Color.white, Color.red, _displayFr / 10f);
        }

        if (satisfactionText) {
            satisfactionText.text = $"Satisfaction: {_displaySa:F1}";
            satisfactionText.color = Color.Lerp(Color.white, Color.green, _displaySa / 10f);
        }

        if (envyText) {
            envyText.text = $"Envy: {_displayEv:F1}";
            envyText.color = Color.Lerp(Color.white, Color.cyan, _displayEv / 10f);
        }
    }
}
