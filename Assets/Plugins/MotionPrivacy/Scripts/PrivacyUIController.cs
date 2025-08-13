using UnityEngine;
using UnityEngine.UI;

public class PrivacyMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public Dropdown appTypeDropdown;   // UGUI Dropdown
    public Slider strengthSlider;
    public Button confirmButton;

    [Header("Strength Range")]
    public float minStrength = 0f;
    public float maxStrength = 0.1f;

    [Header("Privacy Manager GameObjects")]
    public GameObject casualPrivacyManager;      // has MotionPrivacyManager
    public GameObject competitivePrivacyManager; // has MotionPrivacyManager

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(SwitchAndApplyPrivacyManager);
    }

    void SwitchAndApplyPrivacyManager()
    {
        if (appTypeDropdown == null || strengthSlider == null)
        {
            Debug.LogWarning("Assign UI references on PrivacyMenuController.");
            return;
        }

        string selected = appTypeDropdown.options[appTypeDropdown.value].text;
        float strength = Mathf.Lerp(minStrength, maxStrength, strengthSlider.value);

        if (casualPrivacyManager != null) casualPrivacyManager.SetActive(false);
        if (competitivePrivacyManager != null) competitivePrivacyManager.SetActive(false);

        MotionPrivacyManager mgr = null;

        if (selected == "Casual" && casualPrivacyManager != null)
        {
            casualPrivacyManager.SetActive(true);
            mgr = casualPrivacyManager.GetComponent<MotionPrivacyManager>();
            ApplyStrength(mgr, strength * 0.5f);
        }
        else if (selected == "Competitive" && competitivePrivacyManager != null)
        {
            competitivePrivacyManager.SetActive(true);
            mgr = competitivePrivacyManager.GetComponent<MotionPrivacyManager>();
            ApplyStrength(mgr, strength * 1.5f);
        }
        else
        {
            Debug.LogWarning($"Unknown selection '{selected}'.");
        }

        Debug.Log($"Activated {selected} with strength {strength:0.###}");
    }

    void ApplyStrength(MotionPrivacyManager mgr, float s)
    {
        if (mgr == null) return;
        TrySet(mgr.headPositionMechanism, s);
        TrySet(mgr.leftHandMechanism,     s);
        TrySet(mgr.rightHandMechanism,    s);
        TrySet(mgr.eyeGazeMechanism,      s);
    }

    static void TrySet(PositionPrivacyMechanism mech, float v)
    {
        if (mech is IAdjustablePrivacyParam adjustable)
            adjustable.SetParam(v);
    }
}