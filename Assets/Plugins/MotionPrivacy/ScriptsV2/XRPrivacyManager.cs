using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;

namespace XRPrivacy
{
    // Enum for application types
    public enum ApplicationType
    {
        Competitive,
        Casual
    }

    // Main XR Privacy Manager
    public class XRPrivacyManager : MonoBehaviour
    {
        [Header("UI References")]
        public Dropdown applicationTypeDropdown;
        public Slider strengthSlider;
        public Button confirmButton;

        [Header("XR References")]
        public Transform headTransform;
        public Transform leftControllerTransform;
        public Transform rightControllerTransform;

        [Header("Settings")]
        public float maxDisplacement = 0.1f;
        public LayerMask groundCheckLayer = -1;

        [Header("Noise Mechanisms")]
        [Tooltip("Noise mechanism to use for competitive applications")]
        public MonoBehaviour competitiveMechanism;
        [Tooltip("Noise mechanism to use for casual applications")]
        public MonoBehaviour casualMechanism;

        // Private fields
        private INoiseGenerator currentNoiseGenerator;
        private ApplicationType currentApplicationType;
        private float currentStrength;
        private bool privacyEnabled = false;

        // Original positions to prevent accumulation
        private Vector3 originalHeadPosition;
        private Quaternion originalHeadRotation;
        private Vector3 originalLeftControllerPosition;
        private Quaternion originalLeftControllerRotation;
        private Vector3 originalRightControllerPosition;
        private Quaternion originalRightControllerRotation;

        // Current noise offsets (only position now)
        private Vector3 headPositionNoise;
        private Vector3 leftControllerPositionNoise;
        private Vector3 rightControllerPositionNoise;

        void Start()
        {
            InitializeUI();
            StoreOriginalTransforms();
            UpdateCurrentNoiseGenerator();
        }

        void UpdateCurrentNoiseGenerator()
        {
            MonoBehaviour mechanismScript = currentApplicationType == ApplicationType.Competitive ? 
                competitiveMechanism : casualMechanism;

            if (mechanismScript != null && mechanismScript is INoiseGenerator)
            {
                currentNoiseGenerator = mechanismScript as INoiseGenerator;
                Debug.Log($"Using {currentNoiseGenerator.GetMechanismName()} for {currentApplicationType} applications");
            }
            else
            {
                Debug.LogError($"No valid noise mechanism assigned for {currentApplicationType} applications!");
                currentNoiseGenerator = null;
            }
        }

        void InitializeUI()
        {
            // Setup dropdown
            applicationTypeDropdown.ClearOptions();
            applicationTypeDropdown.AddOptions(new List<string> { "Competitive", "Casual" });
            applicationTypeDropdown.onValueChanged.AddListener(OnApplicationTypeChanged);

            // Setup slider
            strengthSlider.minValue = 0f;
            strengthSlider.maxValue = 100f;
            strengthSlider.value = 0f;
            strengthSlider.onValueChanged.AddListener(OnStrengthChanged);

            // Setup button
            confirmButton.onClick.AddListener(OnConfirmClicked);

            // Initialize values
            OnApplicationTypeChanged(0);
            OnStrengthChanged(0f);
        }

        void StoreOriginalTransforms()
        {
            if (headTransform != null)
            {
                originalHeadPosition = headTransform.position;
                originalHeadRotation = headTransform.rotation;
            }
            if (leftControllerTransform != null)
            {
                originalLeftControllerPosition = leftControllerTransform.position;
                originalLeftControllerRotation = leftControllerTransform.rotation;
            }
            if (rightControllerTransform != null)
            {
                originalRightControllerPosition = rightControllerTransform.position;
                originalRightControllerRotation = rightControllerTransform.rotation;
            }
        }

        void Update()
        {
            if (privacyEnabled && currentNoiseGenerator != null)
            {
                ApplyPrivacyNoise();
            }
        }

        void ApplyPrivacyNoise()
        {
            // Update original positions (to handle natural movement)
            UpdateOriginalPositions();

            // Generate new noise
            GenerateNoise();

            // Apply noise with safety checks
            ApplyNoiseToTransforms();
        }

        void UpdateOriginalPositions()
        {
            // Get current XR device positions (without noise) - with error checking
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            
            InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector3 headPos, leftPos, rightPos;
            Quaternion headRot, leftRot, rightRot;

            if (headDevice.isValid && headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out headPos))
                originalHeadPosition = headPos;
            if (headDevice.isValid && headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out headRot))
                originalHeadRotation = headRot;

            if (leftDevice.isValid && leftDevice.TryGetFeatureValue(CommonUsages.devicePosition, out leftPos))
                originalLeftControllerPosition = leftPos;
            if (leftDevice.isValid && leftDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out leftRot))
                originalLeftControllerRotation = leftRot;

            if (rightDevice.isValid && rightDevice.TryGetFeatureValue(CommonUsages.devicePosition, out rightPos))
                originalRightControllerPosition = rightPos;
            if (rightDevice.isValid && rightDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rightRot))
                originalRightControllerRotation = rightRot;
        }

        void GenerateNoise()
        {
            // Generate noise using the current mechanism
            // Head uses eye noise (for head/eye tracking)
            headPositionNoise = currentNoiseGenerator.GenerateEyeNoise(currentStrength);
            
            // Controllers use hand noise
            leftControllerPositionNoise = currentNoiseGenerator.GenerateHandNoise(currentStrength);
            rightControllerPositionNoise = currentNoiseGenerator.GenerateHandNoise(currentStrength);
        }

        void ApplyNoiseToTransforms()
        {
            // Apply head noise (position only, keep original rotation)
            if (headTransform != null)
            {
                Vector3 noisyHeadPosition = originalHeadPosition + headPositionNoise;
                noisyHeadPosition = ClampPosition(noisyHeadPosition, originalHeadPosition);
                
                headTransform.position = noisyHeadPosition;
                headTransform.rotation = originalHeadRotation; // Keep original rotation
            }

            // Apply controller noise (position only, keep original rotation)
            if (leftControllerTransform != null)
            {
                Vector3 noisyLeftPosition = originalLeftControllerPosition + leftControllerPositionNoise;
                noisyLeftPosition = ClampPosition(noisyLeftPosition, originalLeftControllerPosition);
                
                leftControllerTransform.position = noisyLeftPosition;
                leftControllerTransform.rotation = originalLeftControllerRotation; // Keep original rotation
            }

            if (rightControllerTransform != null)
            {
                Vector3 noisyRightPosition = originalRightControllerPosition + rightControllerPositionNoise;
                noisyRightPosition = ClampPosition(noisyRightPosition, originalRightControllerPosition);
                
                rightControllerTransform.position = noisyRightPosition;
                rightControllerTransform.rotation = originalRightControllerRotation; // Keep original rotation
            }
        }

        Vector3 ClampPosition(Vector3 noisyPosition, Vector3 originalPosition)
        {
            // Clamp the displacement to prevent flying off the map
            Vector3 displacement = noisyPosition - originalPosition;
            if (displacement.magnitude > maxDisplacement)
            {
                displacement = displacement.normalized * maxDisplacement;
            }

            Vector3 clampedPosition = originalPosition + displacement;

            // Additional ground check to prevent falling through floor (if enabled)
            if (groundCheckLayer != 0)
            {
                RaycastHit hit;
                if (Physics.Raycast(clampedPosition, Vector3.down, out hit, 10f, groundCheckLayer))
                {
                    if (clampedPosition.y < hit.point.y + 0.1f) // 0.1f buffer above ground
                    {
                        clampedPosition.y = hit.point.y + 0.1f;
                    }
                }
            }

            return clampedPosition;
        }

        // UI Event Handlers
        void OnApplicationTypeChanged(int value)
        {
            currentApplicationType = (ApplicationType)value;
            Debug.Log($"Application Type changed to: {currentApplicationType}");
            
            // Update noise generator when application type changes
            UpdateCurrentNoiseGenerator();
        }

        void OnStrengthChanged(float value)
        {
            currentStrength = value;
            Debug.Log($"Strength changed to: {value:F0}%");
        }

        void OnConfirmClicked()
        {
            privacyEnabled = !privacyEnabled;
            
            if (privacyEnabled)
            {
                Debug.Log($"Privacy enabled - Type: {currentApplicationType}, Mechanism: {currentNoiseGenerator?.GetMechanismName()}, Strength: {currentStrength}%");
                // Get the button text component if it exists
                Text buttonText = confirmButton.GetComponentInChildren<Text>();
                if (buttonText != null) buttonText.text = "Disable";
            }
            else
            {
                Debug.Log("Privacy disabled");
                // Get the button text component if it exists
                Text buttonText = confirmButton.GetComponentInChildren<Text>();
                if (buttonText != null) buttonText.text = "Enable";
                RestoreOriginalPositions();
            }
        }

        void RestoreOriginalPositions()
        {
            if (headTransform != null)
            {
                headTransform.position = originalHeadPosition;
                headTransform.rotation = originalHeadRotation;
            }
            if (leftControllerTransform != null)
            {
                leftControllerTransform.position = originalLeftControllerPosition;
                leftControllerTransform.rotation = originalLeftControllerRotation;
            }
            if (rightControllerTransform != null)
            {
                rightControllerTransform.position = originalRightControllerPosition;
                rightControllerTransform.rotation = originalRightControllerRotation;
            }
        }

        // Public methods for extensibility
        public void SetCompetitiveMechanism(MonoBehaviour mechanismScript)
        {
            if (mechanismScript is INoiseGenerator)
            {
                competitiveMechanism = mechanismScript;
                if (currentApplicationType == ApplicationType.Competitive)
                    UpdateCurrentNoiseGenerator();
            }
            else
            {
                Debug.LogError("Assigned script does not implement INoiseGenerator!");
            }
        }

        public void SetCasualMechanism(MonoBehaviour mechanismScript)
        {
            if (mechanismScript is INoiseGenerator)
            {
                casualMechanism = mechanismScript;
                if (currentApplicationType == ApplicationType.Casual)
                    UpdateCurrentNoiseGenerator();
            }
            else
            {
                Debug.LogError("Assigned script does not implement INoiseGenerator!");
            }
        }
    }
}