using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Toggle and Text
using TMPro; // Required for TextMeshPro text elements

public class PrivacyMenuController : MonoBehaviour
{
    [Header("UI Toggles")]
    public Toggle headPrivacyToggle;
    public Toggle eyePrivacyToggle;
    public Toggle handPrivacyToggle;

    [Header("UI Text Displays (TextMeshPro)")]
    public TextMeshProUGUI headDataText;
    public TextMeshProUGUI eyeDataText;
    public TextMeshProUGUI leftHandDataText;
    public TextMeshProUGUI rightHandDataText;

    // We need a reference to the manager to change its settings
    private MotionPrivacyManager privacyManager;

    // --- INITIALIZATION ---

    void Start()
    {
        // Find the MotionPrivacyManager in the scene
        privacyManager = FindObjectOfType<MotionPrivacyManager>();
        if (privacyManager == null)
        {
            Debug.LogError("PrivacyMenuController could not find a MotionPrivacyManager in the scene!");
            return;
        }

        // Set the initial state of the toggles based on the manager's settings
        InitializeToggles();

        // Add listeners to the toggles so they call our methods when changed
        headPrivacyToggle.onValueChanged.AddListener(OnHeadPrivacyChanged);
        eyePrivacyToggle.onValueChanged.AddListener(OnEyePrivacyChanged);
        handPrivacyToggle.onValueChanged.AddListener(OnHandPrivacyChanged);
    }

    void OnEnable()
    {
        // Subscribe to the data update events to display coordinates
        MotionPrivacyManager.OnPrivatizedHeadUpdate += UpdateHeadText;
        MotionPrivacyManager.OnPrivatizedEyeGazeUpdate += UpdateEyeText;
        MotionPrivacyManager.OnPrivatizedLeftHandUpdate += UpdateLeftHandText;
        MotionPrivacyManager.OnPrivatizedRightHandUpdate += UpdateRightHandText;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent errors
        MotionPrivacyManager.OnPrivatizedHeadUpdate -= UpdateHeadText;
        MotionPrivacyManager.OnPrivatizedEyeGazeUpdate -= UpdateEyeText;
        MotionPrivacyManager.OnPrivatizedLeftHandUpdate -= UpdateLeftHandText;
        MotionPrivacyManager.OnPrivatizedRightHandUpdate -= UpdateRightHandText;
    }

    void InitializeToggles()
    {
        // This is a simplified example. We assume if jitter/quantization is > 0, privacy is "on".
        // You would need to expose the settings from the manager to do this properly.
        // For now, we will just set them to a default state.
        headPrivacyToggle.isOn = true;
        eyePrivacyToggle.isOn = true;
        handPrivacyToggle.isOn = true;

        // Apply initial state
        OnHeadPrivacyChanged(headPrivacyToggle.isOn);
        OnEyePrivacyChanged(eyePrivacyToggle.isOn);
        OnHandPrivacyChanged(handPrivacyToggle.isOn);
    }


    // --- TOGGLE HANDLERS ---

    public void OnHeadPrivacyChanged(bool isOn)
    {
        // To control this, you would need to expose the 'headPositionQuantization'
        // variable in MotionPrivacyManager. For simplicity, we'll just log it.
        Debug.Log("Head Privacy Toggled: " + isOn);
        // Example: privacyManager.SetHeadPrivacy(isOn);
    }

    public void OnEyePrivacyChanged(bool isOn)
    {
        Debug.Log("Eye Privacy Toggled: " + isOn);
        // Example: privacyManager.SetEyePrivacy(isOn);
    }

    public void OnHandPrivacyChanged(bool isOn)
    {
        Debug.Log("Hand Privacy Toggled: " + isOn);
        // Example: privacyManager.SetHandPrivacy(isOn);
    }


    // --- TEXT UPDATE HANDLERS ---

    private void UpdateHeadText(Vector3 rawPos, Vector3 privatePos, Quaternion rotation)
    {
        if (headDataText == null) return;
        string text = $"<b>Head</b>\n" +
                      $"Raw: {rawPos.ToString("F2")}\n" +
                      $"Privatized: {privatePos.ToString("F2")}";
        headDataText.text = text;
    }

    private void UpdateEyeText(Quaternion rawRot, Quaternion privateRot)
    {
        if (eyeDataText == null) return;
        string text = $"<b>Eyes</b>\n" +
                      $"Raw: {rawRot.eulerAngles.ToString("F1")}\n" +
                      $"Privatized: {privateRot.eulerAngles.ToString("F1")}";
        eyeDataText.text = text;
    }

    private void UpdateLeftHandText(Vector3 rawPos, Vector3 privatePos)
    {
        if (leftHandDataText == null) return;
        string text = $"<b>Left Hand</b>\n" +
                      $"Raw: {rawPos.ToString("F2")}\n" +
                      $"Privatized: {privatePos.ToString("F2")}";
        leftHandDataText.text = text;
    }

    private void UpdateRightHandText(Vector3 rawPos, Vector3 privatePos)
    {
        if (rightHandDataText == null) return;
        string text = $"<b>Right Hand</b>\n" +
                      $"Raw: {rawPos.ToString("F2")}\n" +
                      $"Privatized: {privatePos.ToString("F2")}";
        rightHandDataText.text = text;
    }
}
