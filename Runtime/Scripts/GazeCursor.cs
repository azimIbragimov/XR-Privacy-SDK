using UnityEngine;

namespace XRPrivacy
{
    // Gaze reticle for the eye channel. Assign this object to
    // XRPrivacyManager.gazeTransform - the manager places it at the eye and orients it
    // along the (privatized) gaze. This draws a flat disc where the gaze lands, always
    // facing the viewer (a classic VR gaze cursor): clearly visible, never blinding.
    // Builds its own mesh + material at runtime; no setup needed.
    [DisallowMultipleComponent]
    public class GazeCursor : MonoBehaviour
    {
        [Tooltip("Optional ray-origin override. Leave empty - the manager places this object at the eye.")]
        public Transform origin;
        [Tooltip("Maximum gaze ray distance.")]
        public float maxDistance = 30f;
        [Tooltip("Reticle distance when the gaze hits nothing.")]
        public float defaultDistance = 8f;
        [Tooltip("Angular reticle size (world size = this * distance, so apparent size stays constant).")]
        public float size = 0.015f;
        [Tooltip("Minimum world diameter of the reticle, in meters.")]
        public float minSize = 0.04f;
        [Tooltip("Layers the gaze ray can hit.")]
        public LayerMask hitMask = ~0;
        [Tooltip("Reticle color.")]
        public Color color = Color.red;

        private Transform _disc;

        void Start()
        {
            CreateReticle();
        }

        void CreateReticle()
        {
            // A flat cylinder = a disc. Its flat face normal is local +Y, which we aim at
            // the viewer each frame so it reads as a round dot.
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "GazeReticle";

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = go.GetComponent<Renderer>();

            // Prefer the always-on-top overlay shader so the reticle is never occluded by
            // leaves / the dashboard / walls. Loaded from Resources so it is always
            // included in player builds (Shader.Find alone can be stripped). Falls back to
            // recoloring the default (occludable) material if the shader is missing.
            Shader overlay = Shader.Find("XRPrivacy/GazeOverlay");
            if (overlay == null) overlay = Resources.Load<Shader>("GazeOverlay");
            if (overlay != null)
            {
                Material m = new Material(overlay);
                m.SetColor("_Color", color);
                r.material = m;
            }
            else
            {
                ApplyColor(r.material, color);
            }

            _disc = go.transform;
        }

        static void ApplyColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c); }
        }

        void LateUpdate()
        {
            if (_disc == null) return;

            Vector3 originPos = origin != null ? origin.position : transform.position;
            Vector3 dir = transform.forward;

            Vector3 point;
            Vector3 normal = -dir;
            if (Physics.Raycast(originPos, dir, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else
            {
                point = originPos + dir * defaultDistance;
            }

            float dist = Vector3.Distance(originPos, point);
            float diameter = Mathf.Max(minSize, size * dist);

            // Sit just off the surface (toward the viewer) to avoid z-fighting, and face the viewer.
            _disc.position = point + normal * (diameter * 0.05f);
            _disc.up = (originPos - point).sqrMagnitude > 1e-6f ? (originPos - point).normalized : -dir;
            _disc.localScale = new Vector3(diameter, 0.0005f, diameter); // flat disc (cylinder base diameter = 1)
        }

        void OnDestroy()
        {
            if (_disc != null) Destroy(_disc.gameObject);
        }
    }
}
