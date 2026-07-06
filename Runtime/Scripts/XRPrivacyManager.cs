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

        [Header("Whole-body Mechanisms (DMM / MetaGuard body path)")]
        [Tooltip("Tracking-space origin (XR Origin / floor). Whole-body mechanisms (IBodyPrivacyMechanism, e.g. " +
                 "DMM) receive poses in this local play-space to match their training data. If null, world space is used.")]
        public Transform trackingOrigin;
        [Tooltip("If true, the privatized HEAD pose is also written to the head transform (moves the camera/view). " +
                 "Off by default so DMM only privatizes the controllers; the head is still fed to the model for context.")]
        public bool privatizeHead = false;

        [Header("Recording")]
        [Tooltip("Record this session's motion (true + privatized, all channels) to a CSV in persistentDataPath. " +
                 "A MotionRecorder is attached and started automatically when the session begins.")]
        public bool recordSession = false;
        [Tooltip("CSV sample rate (Hz) when Record Session is on.")]
        public float recordSampleHz = 30f;

        [Header("Body Channel Mechanisms (head + controllers, positional)")]
        [Tooltip("Body mechanism to use for competitive applications")]
        public MonoBehaviour competitiveMechanism;
        [Tooltip("Body mechanism to use for casual applications")]
        public MonoBehaviour casualMechanism;
        [Tooltip("Hard cap on how far a body joint may be displaced from its true position, in meters.")]
        public float maxDisplacement = 0.21f;
        [Tooltip("Layers for the body ground check (keeps privatized poses from sinking through the floor).")]
        public LayerMask groundCheckLayer = -1;

        [Header("Eye Channel Mechanisms (gaze, angular, <= Max Eye Angle)")]
        [Tooltip("Eye mechanism to use for competitive applications (an IEyePrivacyMechanism: " +
                 "EyeGaussian / EyeSpatial / EyeSmoothing / EyeTemporal).")]
        public MonoBehaviour competitiveEyeMechanism;
        [Tooltip("Eye mechanism to use for casual applications.")]
        public MonoBehaviour casualEyeMechanism;
        [Tooltip("Transform rotated to the privatized gaze direction (its forward = gaze). e.g. a gaze ray indicator.")]
        public Transform gazeTransform;
        [Tooltip("Hard cap on how far the privatized gaze may deviate from the true gaze, in degrees.")]
        public float maxEyeAngle = 2f;
        [Tooltip("Show a floating readout (in the headset) of the raw eye-tracking state: permission, " +
                 "device found, isTracked, and how far gaze differs from head. For diagnosing eye tracking.")]
        public bool showGazeDebug = false;

        // The eye mechanism active for the current application type.
        private IEyePrivacyMechanism currentEyeMechanism;

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

        // The offset ACTUALLY written last frame, i.e. after clamping. We must
        // re-anchor by subtracting this (not the raw noise), otherwise mechanisms
        // whose raw offset exceeds maxDisplacement (temporal/smoothing/spatial) would
        // subtract more than was applied, drift every frame, and blow up to NaN.
        private Vector3 headAppliedOffset;
        private Vector3 leftControllerAppliedOffset;
        private Vector3 rightControllerAppliedOffset;

        // Rotation deltas actually applied last frame by the whole-body path
        // (privRot = delta * trueRot), used to recover the clean rotation each frame.
        private Quaternion headAppliedRot = Quaternion.identity;
        private Quaternion leftControllerAppliedRot = Quaternion.identity;
        private Quaternion rightControllerAppliedRot = Quaternion.identity;

        // ---- Latest-frame telemetry, exposed for MotionRecorder (world space) ----
        [System.NonSerialized] public BodyPose TruePose;     // clean head/left/right pose
        [System.NonSerialized] public BodyPose PrivPose;     // privatized head/left/right pose
        [System.NonSerialized] public Quaternion TrueGaze = Quaternion.identity;
        [System.NonSerialized] public Quaternion PrivGaze = Quaternion.identity;
        [System.NonSerialized] public bool PrivacyActive;    // body mechanism running this frame
        [System.NonSerialized] public float CurrentStrengthValue;  // slider value 0..100
        [System.NonSerialized] public string CurrentMechanism = "None";
        private BodyPose _recoveredTrue;
        private bool _haveRecovered;

        void Start()
        {
            RequestEyeTrackingPermission();
            StartCoroutine(InitializeWhenXRReady());
        }

        // Quest Pro (and other Meta devices) gate eye data behind a runtime permission.
        // Without it, the eye-tracking device reports isTracked = false and gaze falls
        // back to head-forward. The matching manifest entry
        // (com.oculus.permission.EYE_TRACKING) is auto-added on the Meta Quest build path.
        void RequestEyeTrackingPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string perm = "com.oculus.permission.EYE_TRACKING";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm))
                UnityEngine.Android.Permission.RequestUserPermission(perm);
#endif
        }

        IEnumerator InitializeWhenXRReady()
        {
            // Wait for XR to initialize properly
            float timeout = 10f; // 10 second timeout
            float elapsed = 0f;
            
            while (!IsXRReady() && elapsed < timeout)
            {
                // The XR Device Simulator drives the rig through the Input System
                // with no active XR loader, so IsXRReady() never succeeds. Don't
                // stall the full timeout waiting for a loader that won't appear.
                if (Application.isEditor && XRGeneralSettings.Instance?.Manager?.activeLoader == null && elapsed > 1f)
                    break;

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (IsXRReady())
            {
                Debug.Log("XR is ready, initializing privacy manager...");
            }
            else
            {
                Debug.LogWarning("No active XR loader (e.g. running under the XR Device Simulator) - proceeding anyway.");
            }

            // Proceed regardless so privacy applies to a simulated rig as well as a real headset.
            xrInitialized = true;
            
            InitializeUI();
            yield return new WaitForEndOfFrame(); // Wait one frame for UI to settle
            StoreOriginalTransforms();
            UpdateCurrentNoiseGenerator();
            SetupRecordingIfNeeded();
        }

        // If Record Session is checked, attach (or reuse) a MotionRecorder and start it.
        void SetupRecordingIfNeeded()
        {
            if (!recordSession) return;
            MotionRecorder rec = GetComponent<MotionRecorder>();
            if (rec == null) rec = gameObject.AddComponent<MotionRecorder>();
            rec.manager = this;
            rec.sampleHz = recordSampleHz;
            rec.StartRecording();
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
                // Stateful mechanisms must start clean when (re)selected.
                (currentNoiseGenerator as IPositionPrivacyMechanism)?.ResetState();
                (currentNoiseGenerator as IBodyPrivacyMechanism)?.ResetState();
                // Clear re-anchor deltas so a freshly selected mechanism starts clean.
                headAppliedOffset = leftControllerAppliedOffset = rightControllerAppliedOffset = Vector3.zero;
                headAppliedRot = leftControllerAppliedRot = rightControllerAppliedRot = Quaternion.identity;
                Debug.Log($"Using {currentNoiseGenerator.GetMechanismName()} for {currentApplicationType} applications");
            }
            else
            {
                Debug.LogError($"No valid noise mechanism assigned for {currentApplicationType} applications!");
                currentNoiseGenerator = null;
            }

            // Eye channel: pick the eye mechanism for the current application type too.
            MonoBehaviour eyeScript = currentApplicationType == ApplicationType.Competitive ?
                competitiveEyeMechanism : casualEyeMechanism;
            currentEyeMechanism = eyeScript as IEyePrivacyMechanism;
            currentEyeMechanism?.ResetState();
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

        private int _lastAppliedFrame = -1;

        void OnEnable()  { Application.onBeforeRender += OnBeforeRenderApply; }
        void OnDisable() { Application.onBeforeRender -= OnBeforeRenderApply; }

        // Apply privacy in onBeforeRender, which runs AFTER the XR TrackedPoseDriver has
        // written the true controller/head poses for this frame. Applying in Update would
        // be overwritten by the driver's before-render pose update, so the body privacy
        // would never be visible on driver-controlled controllers.
        void OnBeforeRenderApply()
        {
            if (!xrInitialized) return;
            if (Time.frameCount == _lastAppliedFrame) return; // once per frame (multi-camera safe)
            _lastAppliedFrame = Time.frameCount;

            // Body channel (head + controllers, positional) - only while enabled.
            if (privacyEnabled && currentNoiseGenerator != null)
                ApplyPrivacyNoise();

            // Eye channel: the gaze cursor always tracks the true gaze so it is visible
            // before enabling; the angular perturbation is only applied once enabled.
            ApplyEyePrivacy();

            if (showGazeDebug) UpdateGazeDebug();

            UpdateTelemetry();
        }

        // Capture the latest true/privatized body pose for recording.
        void UpdateTelemetry()
        {
            if (headTransform != null) { PrivPose.headPos = headTransform.position; PrivPose.headRot = headTransform.rotation; }
            if (leftControllerTransform != null) { PrivPose.leftPos = leftControllerTransform.position; PrivPose.leftRot = leftControllerTransform.rotation; }
            if (rightControllerTransform != null) { PrivPose.rightPos = rightControllerTransform.position; PrivPose.rightRot = rightControllerTransform.rotation; }

            PrivacyActive = privacyEnabled && currentNoiseGenerator != null;
            // When privacy is on we have the recovered clean pose; otherwise the rig
            // transforms already hold the true pose.
            TruePose = (PrivacyActive && _haveRecovered) ? _recoveredTrue : PrivPose;
            _haveRecovered = false;

            CurrentStrengthValue = currentStrength;
            CurrentMechanism = currentNoiseGenerator != null ? currentNoiseGenerator.GetMechanismName() : "None";
        }

        void ApplyPrivacyNoise()
        {
            // Whole-body mechanisms (DMM) consume head+hands together, in play space,
            // and privatize rotation too - a separate path from the per-channel pipeline.
            if (currentNoiseGenerator is IBodyPrivacyMechanism bodyMechanism)
            {
                ApplyBodyMechanism(bodyMechanism);
                return;
            }

            // Update original positions (to handle natural movement)
            UpdateOriginalPositions();

            // Generate new noise
            GenerateNoise();

            // Apply noise with safety checks
            ApplyNoiseToTransforms();

            // Per-channel path privatizes position only (rotation is kept); the recovered
            // originals are the clean true pose for recording.
            _recoveredTrue.headPos = originalHeadPosition; _recoveredTrue.headRot = originalHeadRotation;
            _recoveredTrue.leftPos = originalLeftControllerPosition; _recoveredTrue.leftRot = originalLeftControllerRotation;
            _recoveredTrue.rightPos = originalRightControllerPosition; _recoveredTrue.rightRot = originalRightControllerRotation;
            _haveRecovered = true;
        }

        // Whole-body path: recover the clean true poses from the controller transforms,
        // convert to tracking-local space, privatize, convert back, and write the full
        // pose (position + rotation) onto the controllers. To stop the privatization from
        // compounding frame-to-frame, we remove the delta we applied last frame - both
        // position and rotation.
        void ApplyBodyMechanism(IBodyPrivacyMechanism mechanism)
        {
            if (headTransform == null || leftControllerTransform == null || rightControllerTransform == null)
                return;

            BodyPose w;
            RecoverPose(headTransform, headAppliedOffset, headAppliedRot, out w.headPos, out w.headRot);
            RecoverPose(leftControllerTransform, leftControllerAppliedOffset, leftControllerAppliedRot, out w.leftPos, out w.leftRot);
            RecoverPose(rightControllerTransform, rightControllerAppliedOffset, rightControllerAppliedRot, out w.rightPos, out w.rightRot);
            _recoveredTrue = w; _haveRecovered = true;

            BodyPose priv = ToWorld(mechanism.Privatize(ToLocal(w), currentStrength));

            // Controllers are always privatized; the head only if explicitly enabled.
            if (privatizeHead)
                WritePose(headTransform, w.headPos, w.headRot, priv.headPos, priv.headRot, ref headAppliedOffset, ref headAppliedRot);
            WritePose(leftControllerTransform, w.leftPos, w.leftRot, priv.leftPos, priv.leftRot, ref leftControllerAppliedOffset, ref leftControllerAppliedRot);
            WritePose(rightControllerTransform, w.rightPos, w.rightRot, priv.rightPos, priv.rightRot, ref rightControllerAppliedOffset, ref rightControllerAppliedRot);
        }

        void RecoverPose(Transform source, Vector3 appliedOffset, Quaternion appliedRot,
                         out Vector3 truePos, out Quaternion trueRot)
        {
            // Running in onBeforeRender, the driver has already set the clean pose - read
            // it directly (no re-anchoring needed).
            truePos = source.position;
            trueRot = source.rotation;
        }

        void WritePose(Transform source, Vector3 truePos, Quaternion trueRot,
                       Vector3 privPos, Quaternion privRot, ref Vector3 appliedOffset, ref Quaternion appliedRot)
        {
            if (!IsValidPosition(privPos) || !IsValidRotation(privRot)) return;
            source.SetPositionAndRotation(privPos, privRot);
        }

        BodyPose ToLocal(BodyPose w)
        {
            if (trackingOrigin == null) return w;
            Quaternion inv = Quaternion.Inverse(trackingOrigin.rotation);
            BodyPose l;
            l.headPos = trackingOrigin.InverseTransformPoint(w.headPos); l.headRot = inv * w.headRot;
            l.leftPos = trackingOrigin.InverseTransformPoint(w.leftPos); l.leftRot = inv * w.leftRot;
            l.rightPos = trackingOrigin.InverseTransformPoint(w.rightPos); l.rightRot = inv * w.rightRot;
            return l;
        }

        BodyPose ToWorld(BodyPose l)
        {
            if (trackingOrigin == null) return l;
            BodyPose w;
            w.headPos = trackingOrigin.TransformPoint(l.headPos); w.headRot = trackingOrigin.rotation * l.headRot;
            w.leftPos = trackingOrigin.TransformPoint(l.leftPos); w.leftRot = trackingOrigin.rotation * l.leftRot;
            w.rightPos = trackingOrigin.TransformPoint(l.rightPos); w.rightRot = trackingOrigin.rotation * l.rightRot;
            return w;
        }

        void UpdateOriginalPositions()
        {
            // We run in onBeforeRender, AFTER the XR TrackedPoseDriver has written the true
            // pose for this frame, so the transform already holds the clean tracked pose -
            // read it directly. (No re-anchoring needed: the driver re-provides the true
            // pose every frame, so our previous privatized write never compounds.)
            if (headTransform != null)
            {
                Vector3 recovered = GetTrackedPosition(headTransform, originalHeadPosition);
                if (IsValidPosition(recovered)) originalHeadPosition = recovered;
                originalHeadRotation = GetTrackedRotation(headTransform, originalHeadRotation);
            }

            if (leftControllerTransform != null)
            {
                Vector3 recovered = GetTrackedPosition(leftControllerTransform, originalLeftControllerPosition);
                if (IsValidPosition(recovered)) originalLeftControllerPosition = recovered;
                originalLeftControllerRotation = GetTrackedRotation(leftControllerTransform, originalLeftControllerRotation);
            }

            if (rightControllerTransform != null)
            {
                Vector3 recovered = GetTrackedPosition(rightControllerTransform, originalRightControllerPosition);
                if (IsValidPosition(recovered)) originalRightControllerPosition = recovered;
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

            // Position-aware mechanisms (spatial, smoothing, temporal) need the true
            // position rather than just emitting an additive offset. Express their
            // result as an offset so the downstream clamp/ground-check still applies.
            if (currentNoiseGenerator is IPositionPrivacyMechanism posMechanism)
            {
                headPositionNoise = posMechanism.Privatize(XRChannel.Head, originalHeadPosition, currentStrength) - originalHeadPosition;
                leftControllerPositionNoise = posMechanism.Privatize(XRChannel.LeftHand, originalLeftControllerPosition, currentStrength) - originalLeftControllerPosition;
                rightControllerPositionNoise = posMechanism.Privatize(XRChannel.RightHand, originalRightControllerPosition, currentStrength) - originalRightControllerPosition;
                return;
            }

            // Legacy additive mechanisms (e.g. Gaussian) emit a memoryless offset.
            // Body channel: head + both controllers are all positional body telemetry,
            // perturbed identically and capped at maxDisplacement (21 cm). Eye gaze is a
            // separate ANGULAR channel handled by ApplyEyePrivacy(), not here.
            headPositionNoise = currentNoiseGenerator.GenerateBodyNoise(currentStrength);
            leftControllerPositionNoise = currentNoiseGenerator.GenerateBodyNoise(currentStrength);
            rightControllerPositionNoise = currentNoiseGenerator.GenerateBodyNoise(currentStrength);
        }

        // ---- Eye channel: privatize the gaze direction angularly, capped at maxEyeAngle ----
        private Camera _eyeCam;

        // The real XR/head camera. Found under the Head Transform (e.g. Camera Offset ->
        // Main Camera), NOT Camera.main (the scene may have an unrelated tagged camera).
        Camera EyeCamera()
        {
            if (_eyeCam != null) return _eyeCam;
            if (headTransform != null) _eyeCam = headTransform.GetComponentInChildren<Camera>();
            if (_eyeCam == null) _eyeCam = Camera.main;
            return _eyeCam;
        }

        void ApplyEyePrivacy()
        {
            if (gazeTransform == null) return;

            Camera cam = EyeCamera();
            Vector3 eyePos = cam != null ? cam.transform.position
                           : (headTransform != null ? headTransform.position : Vector3.zero);

            Quaternion trueGaze = GetTrueGaze();
            Quaternion priv = trueGaze;

            // Perturb only when privacy is enabled; otherwise the cursor shows true gaze.
            if (privacyEnabled && currentEyeMechanism != null)
            {
                priv = currentEyeMechanism.Privatize(trueGaze, currentStrength);
                // Hard cap: never let the privatized gaze exceed maxEyeAngle from the truth.
                priv = Quaternion.RotateTowards(trueGaze, priv, maxEyeAngle);
            }

            // Place the gaze transform AT the eye, oriented along the privatized gaze, so
            // the cursor rays from the head and tracks the look direction.
            if (IsValidRotation(priv))
                gazeTransform.SetPositionAndRotation(eyePos, priv);

            TrueGaze = trueGaze;
            PrivGaze = priv;
        }

        // Gaze source: prefer a real eye-tracking device, fall back to head forward.
        Quaternion GetTrueGaze()
        {
            if (TryGetEyeGaze(out Quaternion gaze)) return gaze;

            // Head-forward proxy: the real XR camera rotates with the look direction.
            Camera cam = EyeCamera();
            if (cam != null) return cam.transform.rotation;
            if (headTransform != null) return headTransform.rotation;
            return Quaternion.identity;
        }

        // The OpenXR eye-gaze feature (XR_EXT_eye_gaze_interaction, e.g. Quest Pro) exposes
        // a combined "gazeRotation" quaternion on the eye-tracking device - NOT the legacy
        // Eyes/eyesData struct. Referenced by name so this package needs no compile-time
        // dependency on the OpenXR assembly.
        static readonly InputFeatureUsage<Quaternion> s_GazeRotation =
            new InputFeatureUsage<Quaternion>("gazeRotation");

        bool TryGetEyeGaze(out Quaternion gaze)
        {
            gaze = Quaternion.identity;

            // True eye gaze via the eye-tracking device (e.g. Quest Pro / OpenXR).
            List<InputDevice> eyeDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);
            foreach (var d in eyeDevices)
            {
                // Only trust the gaze while the device reports it is actually tracked
                // (eye tracking off / permission denied -> isTracked false).
                bool tracked = !d.TryGetFeatureValue(CommonUsages.isTracked, out bool t) || t;

                // Preferred: OpenXR combined gaze rotation (Quest Pro path).
                if (tracked && d.TryGetFeatureValue(s_GazeRotation, out Quaternion g) && IsValidRotation(g))
                {
                    gaze = ToWorldGaze(g);
                    return true;
                }

                // Legacy XR-SDK per-eye gaze (runtimes that populate eyesData).
                if (tracked && d.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
                {
                    if (eyes.TryGetLeftEyeRotation(out g) && IsValidRotation(g)) { gaze = ToWorldGaze(g); return true; }
                    if (eyes.TryGetRightEyeRotation(out g) && IsValidRotation(g)) { gaze = ToWorldGaze(g); return true; }
                }
            }

            // Coarse fallback: center-eye (HMD) rotation if a runtime exposes it as gaze.
            InputDevice center = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (center.isValid && center.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion c) && IsValidRotation(c))
            {
                gaze = ToWorldGaze(c);
                return true;
            }

            return false;
        }

        // XR Input device rotations are in tracking space; the cursor works in world space.
        // The center-eye (HMD) rotation is in the SAME tracking space, and the render
        // camera is its world-space counterpart - so (camWorld * inverse(headTracking))
        // is the exact tracking->world map. Deriving it from the head this way is robust
        // to however the XR Origin / Camera Offset is positioned or rotated.
        Quaternion ToWorldGaze(Quaternion trackingSpace)
        {
            Camera cam = EyeCamera();
            InputDevice center = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (cam != null && center.isValid &&
                center.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion headTracking) &&
                IsValidRotation(headTracking))
            {
                return cam.transform.rotation * Quaternion.Inverse(headTracking) * trackingSpace;
            }

            // Fallback if the HMD rotation is unavailable.
            return trackingOrigin != null ? trackingOrigin.rotation * trackingSpace : trackingSpace;
        }

        private TextMesh _gazeDebugText;

        // Floating in-headset readout of the raw eye-tracking state, to see exactly why the
        // gaze cursor is / isn't following the eyes. Toggle with showGazeDebug.
        void UpdateGazeDebug()
        {
            Camera cam = EyeCamera();
            if (cam == null) return;

            if (_gazeDebugText == null)
            {
                var go = new GameObject("GazeDebug");
                _gazeDebugText = go.AddComponent<TextMesh>();
                _gazeDebugText.characterSize = 0.02f;
                _gazeDebugText.fontSize = 90;
                _gazeDebugText.anchor = TextAnchor.MiddleCenter;
                _gazeDebugText.color = Color.yellow;
            }
            // Park it 1.5 m in front of the eye, facing the user.
            Transform tf = _gazeDebugText.transform;
            tf.position = cam.transform.position + cam.transform.forward * 1.5f - cam.transform.up * 0.4f;
            tf.rotation = Quaternion.LookRotation(tf.position - cam.transform.position, cam.transform.up);

            // Permission state (device only).
            string perm = "n/a (editor)";
#if UNITY_ANDROID && !UNITY_EDITOR
            perm = UnityEngine.Android.Permission.HasUserAuthorizedPermission("com.oculus.permission.EYE_TRACKING")
                 ? "GRANTED" : "DENIED";
#endif

            // Raw eye device state.
            var eyeDevices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);
            string devInfo;
            if (eyeDevices.Count == 0)
            {
                devInfo = "eye device: NONE FOUND";
            }
            else
            {
                var d = eyeDevices[0];
                bool hasTracked = d.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
                bool hasGaze = d.TryGetFeatureValue(s_GazeRotation, out Quaternion g);

                // Angle between eye gaze and head, in tracking space. ~0 always => eyes not
                // producing independent data (head-locked privacy fallback).
                float eyeVsHead = -1f;
                InputDevice center = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (hasGaze && center.isValid &&
                    center.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion head))
                    eyeVsHead = Vector3.Angle(g * Vector3.forward, head * Vector3.forward);

                devInfo = $"eye device: '{d.name}'\n" +
                          $"isTracked: {(hasTracked ? tracked.ToString() : "no feature")}\n" +
                          $"gazeRotation: {(hasGaze ? "yes" : "MISSING")}\n" +
                          $"eye-vs-head: {(eyeVsHead < 0 ? "n/a" : eyeVsHead.ToString("F1") + " deg")}";
            }

            _gazeDebugText.text = $"EYE TRACKING DEBUG\nperm: {perm}\n{devInfo}";
        }

        void ApplyNoiseToTransforms()
        {
            // Apply head noise (position only, keep original rotation)
            if (headTransform != null)
            {
                Vector3 noisyHeadPosition = originalHeadPosition + headPositionNoise;
                noisyHeadPosition = ClampPosition(noisyHeadPosition, originalHeadPosition);

                if (IsValidPosition(noisyHeadPosition))
                {
                    // Remember the clamped offset we actually applied so next frame's
                    // re-anchor subtracts exactly what was added (no drift).
                    headAppliedOffset = noisyHeadPosition - originalHeadPosition;
                    headTransform.position = noisyHeadPosition;
                    headTransform.rotation = originalHeadRotation; // Keep original rotation
                }
            }

            // Apply controller noise (position only, keep original rotation)
            if (leftControllerTransform != null)
            {
                Vector3 noisyLeftPosition = originalLeftControllerPosition + leftControllerPositionNoise;
                noisyLeftPosition = ClampPosition(noisyLeftPosition, originalLeftControllerPosition);

                if (IsValidPosition(noisyLeftPosition))
                {
                    leftControllerAppliedOffset = noisyLeftPosition - originalLeftControllerPosition;
                    leftControllerTransform.position = noisyLeftPosition;
                    leftControllerTransform.rotation = originalLeftControllerRotation; // Keep original rotation
                }
            }

            if (rightControllerTransform != null)
            {
                Vector3 noisyRightPosition = originalRightControllerPosition + rightControllerPositionNoise;
                noisyRightPosition = ClampPosition(noisyRightPosition, originalRightControllerPosition);

                if (IsValidPosition(noisyRightPosition))
                {
                    rightControllerAppliedOffset = noisyRightPosition - originalRightControllerPosition;
                    rightControllerTransform.position = noisyRightPosition;
                    rightControllerTransform.rotation = originalRightControllerRotation; // Keep original rotation
                }
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
                // Clear stateful mechanism history so re-enabling starts fresh.
                (currentNoiseGenerator as IPositionPrivacyMechanism)?.ResetState();
                (currentNoiseGenerator as IBodyPrivacyMechanism)?.ResetState();
                currentEyeMechanism?.ResetState();
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

            // Clear the stored noise so the next enable re-anchors to the true pose
            // instead of subtracting a stale offset from a now-clean transform.
            headPositionNoise = Vector3.zero;
            leftControllerPositionNoise = Vector3.zero;
            rightControllerPositionNoise = Vector3.zero;
            headAppliedOffset = Vector3.zero;
            leftControllerAppliedOffset = Vector3.zero;
            rightControllerAppliedOffset = Vector3.zero;
            headAppliedRot = leftControllerAppliedRot = rightControllerAppliedRot = Quaternion.identity;
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