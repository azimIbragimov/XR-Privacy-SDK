using System.Text;
using Unity.InferenceEngine;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UI;
using XRPrivacy;

namespace XRPrivacy.EditorTools
{
    // One-click scene setup. Imports the XR Interaction Toolkit Starter Assets + XR Device
    // Simulator samples (matched to the installed XRI version - nothing is redistributed),
    // then creates the XR rig, the XRPrivacyManager, the Dashboard GUI, one of each
    // mechanism, and the gaze cursor, fully wired. Menu: Tools > XR-Privacy > Set Up Scene.
    public static class XRPrivacySetupWizard
    {
        const string XriPackage = "com.unity.xr.interaction.toolkit";
        const string Pkg = "Packages/com.xrprivacy.sdk/Runtime";
        const string DashboardPath = Pkg + "/Dashboard.prefab";
        const string AnonPath = Pkg + "/Models/anonymizer.onnx";
        const string NormPath = Pkg + "/Models/normalizer.onnx";
        const string UndoName = "XR-Privacy Scene Setup";

        [MenuItem("Tools/XR-Privacy/Set Up Scene")]
        public static void SetUpScene()
        {
            // The Dashboard uses TextMeshPro, which needs its Essential Resources imported
            // once per project. (No recompile, so this is safe to do inline.)
            EnsureTmpEssentials();

            // Phase 0: make sure the XRI samples we rely on (rig + simulator) are imported.
            // Importing recompiles the project, so if we trigger an import we stop here and
            // ask the user to re-run once Unity finishes compiling.
            if (EnsureXriSamples())
                return;

            var log = new StringBuilder();

            // 1) Manager.
            var managerGO = new GameObject("XRPrivacyManager");
            Undo.RegisterCreatedObjectUndo(managerGO, UndoName);
            var manager = managerGO.AddComponent<XRPrivacyManager>();

            // 2) Rig: reuse an existing XR Origin, else instantiate the Starter Assets rig.
            Camera cam = null;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                var rigPrefab = FindPrefabByName("XR Origin (XR Rig)");
                if (rigPrefab != null)
                {
                    var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                    Undo.RegisterCreatedObjectUndo(rig, UndoName);
                    origin = rig.GetComponentInChildren<XROrigin>(true);
                    log.AppendLine("Rig: instantiated 'XR Origin (XR Rig)'.");
                }
                else if (EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)"))
                {
                    origin = Object.FindAnyObjectByType<XROrigin>();
                    log.AppendLine("Rig: created a basic 'XR Origin (VR)' (Starter Assets rig not found).");
                }
            }

            if (origin != null)
            {
                manager.trackingOrigin = origin.transform;
                cam = origin.Camera != null ? origin.Camera : Camera.main;
                if (cam != null) manager.headTransform = cam.transform;
                manager.leftControllerTransform = FindByName(origin.transform, "left", "controller");
                manager.rightControllerTransform = FindByName(origin.transform, "right", "controller");
                log.AppendLine($"Rig: ready (head={OK(manager.headTransform)}, " +
                               $"left={OK(manager.leftControllerTransform)}, right={OK(manager.rightControllerTransform)})");
            }
            else
            {
                log.AppendLine("Rig: could not find or create an XR Origin.");
            }

            // 3) XR Device Simulator for mouse/keyboard testing (sample is imported by now).
            if (GameObject.Find("XR Device Simulator") == null)
            {
                var simPrefab = FindPrefabByName("XR Device Simulator");
                if (simPrefab != null)
                {
                    var sim = (GameObject)PrefabUtility.InstantiatePrefab(simPrefab);
                    Undo.RegisterCreatedObjectUndo(sim, UndoName);
                    log.AppendLine("Sim: added XR Device Simulator for editor testing.");
                }
            }

            // 4) Dashboard GUI, wired to the manager's UI references.
            var dashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DashboardPath);
            if (dashPrefab != null)
            {
                var dash = (GameObject)PrefabUtility.InstantiatePrefab(dashPrefab);
                Undo.RegisterCreatedObjectUndo(dash, UndoName);
                dash.name = "XR Privacy Dashboard";
                manager.applicationTypeDropdown = dash.GetComponentInChildren<Dropdown>(true);
                manager.strengthSlider = dash.GetComponentInChildren<Slider>(true);
                manager.confirmButton = dash.GetComponentInChildren<Button>(true);

                // Place it a comfortable distance in front of the camera, facing the user,
                // instead of leaving it at the prefab's saved (far-away) world position.
                if (cam != null)
                {
                    const float dist = 1.5f;
                    Vector3 p = cam.transform.position + cam.transform.forward * dist;
                    dash.transform.position = p;
                    dash.transform.rotation = Quaternion.LookRotation(p - cam.transform.position, Vector3.up);
                }

                log.AppendLine($"Dashboard: instantiated (dropdown={OK(manager.applicationTypeDropdown)}, " +
                               $"slider={OK(manager.strengthSlider)}, button={OK(manager.confirmButton)})");
            }
            else
            {
                log.AppendLine("Dashboard: prefab not found at " + DashboardPath);
            }

            // 5) Body mechanisms.
            var mechParent = new GameObject("Privacy Mechanisms");
            Undo.RegisterCreatedObjectUndo(mechParent, UndoName);

            AddMech<GaussianNoise>(mechParent, "GaussianNoise");
            AddMech<NoMechanism>(mechParent, "NoMechanism");
            AddMech<SpatialNoise>(mechParent, "SpatialNoise");
            AddMech<SmoothingMechanism>(mechParent, "SmoothingMechanism");
            AddMech<TemporalNoise>(mechParent, "TemporalNoise");

            var metaguard = AddMech<MetaGuardMechanism>(mechParent, "MetaGuard");
            if (origin != null) metaguard.trackingOrigin = origin.transform;

            var dmm = AddMech<DMMMechanism>(mechParent, "DMM");
            dmm.anonymizerModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(AnonPath);
            dmm.normalizerModel = AssetDatabase.LoadAssetAtPath<ModelAsset>(NormPath);
            log.AppendLine($"DMM: models (anonymizer={OK(dmm.anonymizerModel)}, normalizer={OK(dmm.normalizerModel)})");

            AddMech<DMMGaussian>(mechParent, "DMM-Gaussian").dmm = dmm;
            var dmmSpatial = AddMech<DMMSpatial>(mechParent, "DMM-Spatial"); dmmSpatial.dmm = dmm;
            var dmmSmoothing = AddMech<DMMSmoothing>(mechParent, "DMM-Smoothing"); dmmSmoothing.dmm = dmm;
            AddMech<DMMTemporal>(mechParent, "DMM-Temporal").dmm = dmm;

            // Defaults: Competitive = DMM-Spatial body, Casual = DMM-Smoothing body.
            manager.competitiveMechanism = dmmSpatial;
            manager.casualMechanism = dmmSmoothing;

            // 6) Eye channel: EyeSmoothing for both modes (+ spare eye mechanisms) + cursor.
            var eyeSmoothing = AddMech<EyeSmoothing>(mechParent, "EyeSmoothing");
            AddMech<EyeGaussian>(mechParent, "EyeGaussian");
            AddMech<EyeSpatial>(mechParent, "EyeSpatial");
            AddMech<EyeTemporal>(mechParent, "EyeTemporal");
            manager.competitiveEyeMechanism = eyeSmoothing;
            manager.casualEyeMechanism = eyeSmoothing;

            var gazeGO = new GameObject("Gaze Cursor");
            Undo.RegisterCreatedObjectUndo(gazeGO, UndoName);
            gazeGO.AddComponent<GazeCursor>();
            manager.gazeTransform = gazeGO.transform;
            log.AppendLine("Eye: EyeGaussian + Gaze Cursor created and wired.");

            EditorUtility.SetDirty(manager);
            Selection.activeGameObject = managerGO;
            Debug.Log("[XR-Privacy] Scene setup complete.\n" + log);

            EditorUtility.DisplayDialog("XR-Privacy Setup",
                "Done! The XR rig, privacy manager, dashboard, all mechanisms, and the gaze " +
                "cursor are set up and wired.\n\nPress Play, set strength to 100, and click Confirm. " +
                "See the Console for a setup summary.",
                "OK");
        }

        // Imports the XRI samples we depend on if they aren't present. Returns true if it
        // triggered an import (caller should stop and let Unity recompile first).
        static bool EnsureXriSamples()
        {
            bool needRig = FindPrefabByName("XR Origin (XR Rig)") == null;
            bool needSim = FindPrefabByName("XR Device Simulator") == null;
            if (!needRig && !needSim) return false;

            bool imported = false;
            if (needRig) imported |= ImportSample("Starter Assets");
            if (needSim) imported |= ImportSample("XR Device Simulator");

            if (!imported) return false;

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("XR-Privacy Setup",
                "Imported the XR Interaction Toolkit Starter Assets + XR Device Simulator " +
                "(matched to your installed XRI version).\n\nUnity will recompile now — when it " +
                "finishes, run Tools > XR-Privacy > Set Up Scene again to build the scene.",
                "OK");
            return true;
        }

        // Import TextMeshPro Essential Resources (bundled in com.unity.ugui) if they aren't
        // already present, so the Dashboard's text renders correctly.
        static void EnsureTmpEssentials()
        {
            if (System.IO.Directory.Exists("Assets/TextMesh Pro")) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");
            if (info == null) return;
            string pkg = System.IO.Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (System.IO.File.Exists(pkg))
            {
                AssetDatabase.ImportPackage(pkg, false);
                Debug.Log("[XR-Privacy] Imported TextMesh Pro Essential Resources.");
            }
        }

        static bool ImportSample(string sampleName)
        {
            foreach (var s in Sample.FindByPackage(XriPackage, string.Empty))
            {
                if (s.displayName != sampleName) continue;
                if (s.isImported) return false;
                return s.Import(Sample.ImportOptions.OverridePreviousImports);
            }
            return false;
        }

        static T AddMech<T>(GameObject parent, string name) where T : Component
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<T>();
        }

        static GameObject FindPrefabByName(string assetName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"\"{assetName}\" t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static Transform FindByName(Transform root, params string[] needles)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                bool all = true;
                foreach (var needle in needles)
                    if (!n.Contains(needle)) { all = false; break; }
                if (all) return t;
            }
            return null;
        }

        static string OK(Object o) => o != null ? "ok" : "MISSING";
    }
}
