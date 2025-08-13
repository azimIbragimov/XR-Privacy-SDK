using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
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
        private bool xrInitialized = false;

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
            StartCoroutine(InitializeWhenXRReady());
        }

        IEnumerator InitializeWhenXRReady()
        {
            // Wait for XR to initialize properly
            float timeout = 10f; // 10 second timeout
            float elapsed = 0f;
            
            while (!IsXRReady() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning("XR initialization timed out, proceeding anyway...");
            }
            else
            {
                Debug.Log("XR is ready, initializing privacy manager...");
                xrInitialized = true;
            }
            
            InitializeUI();
            yield return new WaitForEndOfFrame(); // Wait one frame for UI to settle
            StoreOriginalTransforms();
            UpdateCurrentNoiseGenerator();
        }

        bool IsXRReady()
        {
            // Check if XR subsystem is running
            var xrManager = XRGeneralSettings.Instance?.Manager;
            if (xrManager?.activeLoader == null) return false;
            
            var inputSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>();
            return inputSubsystem?.running == true;
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
            if (applicationTypeDropdown != null)
            {
                applicationTypeDropdown.ClearOptions();
                applicationTypeDropdown.AddOptions(new List<string> { "Competitive", "Casual" });
                applicationTypeDropdown.onValueChanged.AddListener(OnApplicationTypeChanged);
            }

            // Setup slider
            if (strengthSlider != null)
            {
                strengthSlider.minValue = 0f;
                strengthSlider.maxValue = 100f;
                strengthSlider.value = 0f;
                strengthSlider.onValueChanged.AddListener(OnStrengthChanged);
            }

            // Setup button
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            // Initialize values
            OnApplicationTypeChanged(0);
            OnStrengthChanged(0f);
        }

        void StoreOriginalTransforms()
        {
            Debug.Log("Storing original transforms...");
            
            if (headTransform != null)
            {
                originalHeadPosition = headTransform.position;
                originalHeadRotation = headTransform.rotation;
                Debug.Log($"Head original position: {originalHeadPosition}");
            }
            if (leftControllerTransform != null)
            {
                originalLeftControllerPosition = leftControllerTransform.position;
                originalLeftControllerRotation = leftControllerTransform.rotation;
                Debug.Log($"Left controller original position: {originalLeftControllerPosition}");
            }
            if (rightControllerTransform != null)
            {
                originalRightControllerPosition = rightControllerTransform.position;
                originalRightControllerRotation = rightControllerTransform.rotation;
                Debug.Log($"Right controller original position: {originalRightControllerPosition}");
            }
        }

        void Update()
        {
            if (privacyEnabled && currentNoiseGenerator != null && xrInitialized)
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
            // Use transform positions directly instead of XR Input API
            // This is more reliable as transforms are already in world space and tracked
            if (headTransform != null)
            {
                originalHeadPosition = GetTrackedPosition(headTransform, originalHeadPosition);
                originalHeadRotation = GetTrackedRotation(headTransform, originalHeadRotation);
            }

            if (leftControllerTransform != null)
            {
                originalLeftControllerPosition = GetTrackedPosition(leftControllerTransform, originalLeftControllerPosition);
                originalLeftControllerRotation = GetTrackedRotation(leftControllerTransform, originalLeftControllerRotation);
            }

            if (rightControllerTransform != null)
            {
                originalRightControllerPosition = GetTrackedPosition(rightControllerTransform, originalRightControllerPosition);
                originalRightControllerRotation = GetTrackedRotation(rightControllerTransform, originalRightControllerRotation);
            }
        }

        Vector3 GetTrackedPosition(Transform transform, Vector3 fallback)
        {
            // Check if the position is valid (not zero or NaN)
            Vector3 pos = transform.position;
            if (IsValidPosition(pos))
            {
                return pos;
            }
            
            // Try to get position from XR Input API as fallback
            if (TryGetXRPosition(transform, out Vector3 xrPos))
            {
                return xrPos;
            }
            
            // Return previous valid position as last resort
            return fallback;
        }

        Quaternion GetTrackedRotation(Transform transform, Quaternion fallback)
        {
            // Check if the rotation is valid
            Quaternion rot = transform.rotation;
            if (IsValidRotation(rot))
            {
                return rot;
            }
            
            // Try to get rotation from XR Input API as fallback
            if (TryGetXRRotation(transform, out Quaternion xrRot))
            {
                return xrRot;
            }
            
            // Return previous valid rotation as last resort
            return fallback;
        }

        bool TryGetXRPosition(Transform transform, out Vector3 position)
        {
            position = Vector3.zero;
            
            XRNode node = GetXRNodeForTransform(transform);
            if (node == XRNode.TrackingReference) return false;
            
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                // Check if device is tracked
                if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && isTracked)
                {
                    if (device.TryGetFeatureValue(CommonUsages.devicePosition, out position))
                    {
                        return IsValidPosition(position);
                    }
                }
            }
            
            return false;
        }

        bool TryGetXRRotation(Transform transform, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            
            XRNode node = GetXRNodeForTransform(transform);
            if (node == XRNode.TrackingReference) return false;
            
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                // Check if device is tracked
                if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && isTracked)
                {
                    if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                    {
                        return IsValidRotation(rotation);
                    }
                }
            }
            
            return false;
        }

        XRNode GetXRNodeForTransform(Transform transform)
        {
            if (transform == headTransform) return XRNode.Head;
            if (transform == leftControllerTransform) return XRNode.LeftHand;
            if (transform == rightControllerTransform) return XRNode.RightHand;
            return XRNode.TrackingReference; // Invalid
        }

        bool IsValidPosition(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) && 
                   !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }

        bool IsValidRotation(Quaternion rot)
        {
            return !float.IsNaN(rot.x) && !float.IsNaN(rot.y) && !float.IsNaN(rot.z) && !float.IsNaN(rot.w) &&
                   !float.IsInfinity(rot.x) && !float.IsInfinity(rot.y) && !float.IsInfinity(rot.z) && !float.IsInfinity(rot.w);
        }

        void GenerateNoise()
        {
            if (currentNoiseGenerator == null) return;
            
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

        // Debug method to check XR status
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void LogXRStatus()
        {
            Debug.Log($"XR Initialized: {xrInitialized}");
            Debug.Log($"XR Manager Active: {XRGeneralSettings.Instance?.Manager?.activeLoader != null}");
            
            var inputSubsystem = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRInputSubsystem>();
            Debug.Log($"Input Subsystem Running: {inputSubsystem?.running}");
            
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            Debug.Log($"Connected XR Devices: {devices.Count}");
            
            foreach (var device in devices)
            {
                Debug.Log($"Device: {device.name} - Valid: {device.isValid} - Characteristics: {device.characteristics}");
            }
        }
    }
}