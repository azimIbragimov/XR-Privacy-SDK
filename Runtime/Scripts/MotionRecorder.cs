using System.Globalization;
using System.IO;
using UnityEngine;

namespace XRPrivacy
{
    // Records head/controller/gaze motion to a CSV file. Logs BOTH the true (clean) and
    // privatized pose each sample, so recordings can be compared or fed to an evaluation
    // pipeline. Add to any GameObject; it reads the live telemetry the XRPrivacyManager
    // exposes each frame.
    //
    // Output: Application.persistentDataPath/<prefix>_<timestamp>.csv  (path logged to the
    // Console on start). Toggle with the record key, or enable "Record On Play".
    public class MotionRecorder : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The privacy manager to record from. Auto-found if left empty.")]
        public XRPrivacyManager manager;

        [Header("Recording")]
        [Tooltip("Samples per second written to the CSV.")]
        public float sampleHz = 30f;
        [Tooltip("Start recording automatically when entering Play mode.")]
        public bool recordOnPlay = false;
        [Tooltip("Key to start/stop recording.")]
        public KeyCode toggleKey = KeyCode.R;
        [Tooltip("File name prefix; a timestamp + .csv is appended.")]
        public string fileNamePrefix = "xrprivacy";

        private StreamWriter _writer;
        private bool _recording;
        private float _accum;
        private float _startTime;
        private string _path;

        public bool IsRecording => _recording;
        public string LastPath => _path;

        void Reset() { manager = FindAnyObjectByType<XRPrivacyManager>(); }

        void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<XRPrivacyManager>();
            if (recordOnPlay) StartRecording();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        // LateUpdate: the manager has already produced this frame's telemetry in Update.
        void LateUpdate()
        {
            if (!_recording || manager == null) return;
            _accum += Time.deltaTime;
            float dt = 1f / Mathf.Max(1f, sampleHz);
            if (_accum < dt) return;
            _accum = 0f;
            WriteRow();
        }

        public void Toggle()
        {
            if (_recording) StopRecording();
            else StartRecording();
        }

        public void StartRecording()
        {
            if (_recording) return;
            if (manager == null) manager = FindAnyObjectByType<XRPrivacyManager>();

            string name = $"{fileNamePrefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            _path = Path.Combine(Application.persistentDataPath, name);
            _writer = new StreamWriter(_path, false);
            _writer.WriteLine(Header());
            _startTime = Time.time;
            _accum = float.MaxValue; // write the first sample immediately
            _recording = true;
            Debug.Log($"[MotionRecorder] Recording to: {_path}");
        }

        public void StopRecording()
        {
            if (!_recording) return;
            _recording = false;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            Debug.Log($"[MotionRecorder] Stopped. Saved: {_path}");
        }

        void OnDisable() => StopRecording();
        void OnApplicationQuit() => StopRecording();

        string Header()
        {
            const string body = "px,py,pz,qx,qy,qz,qw";
            return "time,privacy_active,mechanism,strength," +
                   Prefix("head_true_", body) + "," + Prefix("head_priv_", body) + "," +
                   Prefix("left_true_", body) + "," + Prefix("left_priv_", body) + "," +
                   Prefix("right_true_", body) + "," + Prefix("right_priv_", body) + "," +
                   "gaze_true_qx,gaze_true_qy,gaze_true_qz,gaze_true_qw," +
                   "gaze_priv_qx,gaze_priv_qy,gaze_priv_qz,gaze_priv_qw";
        }

        static string Prefix(string p, string cols)
        {
            string[] parts = cols.Split(',');
            for (int i = 0; i < parts.Length; i++) parts[i] = p + parts[i];
            return string.Join(",", parts);
        }

        void WriteRow()
        {
            BodyPose t = manager.TruePose;
            BodyPose p = manager.PrivPose;
            var ci = CultureInfo.InvariantCulture;

            string row = string.Join(",",
                (Time.time - _startTime).ToString("F4", ci),
                manager.PrivacyActive ? "1" : "0",
                manager.CurrentMechanism,
                manager.CurrentStrengthValue.ToString("F2", ci),
                Pose(t.headPos, t.headRot), Pose(p.headPos, p.headRot),
                Pose(t.leftPos, t.leftRot), Pose(p.leftPos, p.leftRot),
                Pose(t.rightPos, t.rightRot), Pose(p.rightPos, p.rightRot),
                Quat(manager.TrueGaze), Quat(manager.PrivGaze));

            _writer.WriteLine(row);
        }

        static string Pose(Vector3 v, Quaternion q)
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Join(",",
                v.x.ToString("F5", ci), v.y.ToString("F5", ci), v.z.ToString("F5", ci),
                q.x.ToString("F5", ci), q.y.ToString("F5", ci), q.z.ToString("F5", ci), q.w.ToString("F5", ci));
        }

        static string Quat(Quaternion q)
        {
            var ci = CultureInfo.InvariantCulture;
            return string.Join(",",
                q.x.ToString("F5", ci), q.y.ToString("F5", ci), q.z.ToString("F5", ci), q.w.ToString("F5", ci));
        }
    }
}
