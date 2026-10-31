using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds "Tools > Create Fidget Spinner" to the Unity menu bar.
// Builds a ~20 cm, three-lobed spinner in the open scene and tries to turn on
// physics capsules for any OVRSkeleton hands so your fingers can push the blades.
public static class CreateFidgetSpinner
{
    const float ArmRadius = 0.08f;   // hub center -> weight center (m)
    const float Thickness = 0.012f;  // spinner thickness (m)

    [MenuItem("Tools/Create Fidget Spinner")]
    public static void Create()
    {
        var bodyMat = MakeMat("Spinner_Body", new Color(0.10f, 0.55f, 0.95f), 0.8f, 0.2f);
        var metalMat = MakeMat("Spinner_Metal", new Color(0.80f, 0.80f, 0.85f), 0.9f, 1.0f);

        var root = new GameObject("Fidget Spinner");
        Undo.RegisterCreatedObjectUndo(root, "Create Fidget Spinner");
        root.transform.position = new Vector3(0f, 1.1f, 0.4f); // ~chest height, in front of the rig

        // Hub (center bearing)
        Part(PrimitiveType.Cylinder, "Hub", root.transform, Vector3.zero, Quaternion.identity,
             new Vector3(0.05f, Thickness * 0.6f, 0.05f), metalMat);

        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            var rot = Quaternion.Euler(0f, a, 0f);
            Vector3 dir = rot * Vector3.forward;

            // Arm connecting hub to weight
            Part(PrimitiveType.Cube, "Arm " + (i + 1), root.transform, dir * (ArmRadius * 0.5f), rot,
                 new Vector3(0.03f, Thickness, ArmRadius), bodyMat);

            // Lobe (outer ring) and weight (bearing) at the tip
            Part(PrimitiveType.Cylinder, "Lobe " + (i + 1), root.transform, dir * ArmRadius, Quaternion.identity,
                 new Vector3(0.05f, Thickness * 0.5f, 0.05f), bodyMat);
            Part(PrimitiveType.Cylinder, "Weight " + (i + 1), root.transform, dir * ArmRadius, Quaternion.identity,
                 new Vector3(0.03f, Thickness * 0.6f, 0.03f), metalMat);
        }

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.useGravity = false;
        root.AddComponent<FidgetSpinner>();

        EnableHandCapsules();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Fidget Spinner created at (0, 1.1, 0.4). Move it if it's not in a comfy spot.");
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                           Quaternion localRot, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        if (type == PrimitiveType.Cylinder)
        {
            // Default cylinder collider is a capsule (becomes a fat sphere when flattened); use the real shape.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
        }
        return go;
    }

    static Material MakeMat(string name, Color c, float smoothness, float metallic)
    {
        const string folder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Materials");
        string path = folder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", c);
        m.color = c;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Best effort: flip "Enable Physics Capsules" on every OVRSkeleton in the scene.
    static void EnableHandCapsules()
    {
        int found = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "OVRSkeleton") continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("_enablePhysicsCapsules");
            if (prop == null) continue;
            prop.boolValue = true;
            so.ApplyModifiedProperties();
            found++;
        }
        if (found > 0)
            Debug.Log($"Enabled physics capsules on {found} OVRSkeleton hand(s).");
        else
            Debug.LogWarning("No OVRSkeleton hands found. Add the Camera Rig + Hand Tracking building blocks, " +
                             "then run Tools > Create Fidget Spinner again (or give your hands colliders manually).");
    }
}
