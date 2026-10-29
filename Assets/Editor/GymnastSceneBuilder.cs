using GymnastGame.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu that turns the open scene into the real-scale 3D high bar scene:
/// mat top at y = 0, bar 2.6 m above it (bar axis along Z), perspective camera that follows the gymnast,
/// the articulated figure (bones with Humanoid names, sizes from body.json) and the gameplay components
/// (GymnastController, GymnastView, RoundFlow) wired together.
/// Safe to run again: objects are found by name and updated, missing ones are created.
/// </summary>
public static class GymnastSceneBuilder
{
    private const float BarHeight = 2.6f;        // above the top of the mat
    private const float BarLength = 2.4f;
    private const float BarDiameter = 0.028f;
    private const float MatThickness = 0.2f;
    private const string MaterialFolder = "Assets/Materials";
    private const string BodyDataPath = "Assets/Data/Body/body.json";
    private const string DeductionDataPath = "Assets/Data/HighBar/deductions.json";

    // The trunk is wider than it is deep; its radius in body.json is the average.
    private const float TrunkDepthFactor = 0.8f, TrunkWidthFactor = 1.17f;

    [MenuItem("Tools/Gymnast/Build 3D Scene")]
    public static void Build()
    {
        var bar = Require("Bar");
        var gymnastObject = Require("Gymnast");
        if (bar == null || gymnastObject == null) return;

        Undo.SetCurrentGroupName("Build 3D Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // The bar is the pivot of the swing. It no longer rotates: GymnastView places the body.
        Undo.RecordObject(bar.transform, "Bar");
        bar.transform.SetPositionAndRotation(new Vector3(0f, BarHeight, 0f), Quaternion.identity);

        // Old GymnastSwing / GymnastDismount scripts are deleted and show up as "Missing Script".
        Undo.RegisterCompleteObjectUndo(bar, "Bar");
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(bar);

        var bodyData = AssetDatabase.LoadAssetAtPath<TextAsset>(BodyDataPath);
        if (bodyData == null)
        {
            Debug.LogError($"Missing {BodyDataPath}.");
            return;
        }
        var body = BodyModel.FromJson(bodyData.text);
        var deductionData = AssetDatabase.LoadAssetAtPath<TextAsset>(DeductionDataPath);

        var gymnast = gymnastObject.transform;
        if (gymnast.parent != null) Undo.SetTransformParent(gymnast, null, "Gymnast");
        Place(gymnastObject, new Vector3(0f, BarHeight - 1f, 0f), Quaternion.identity, Vector3.one);
        BuildFigure(gymnastObject, body);

        var controller = GetOrAdd<GymnastController>(bar);
        // Inspector values are stored in the scene, so changing the defaults in code is not enough.
        var controllerData = new SerializedObject(controller);
        controllerData.FindProperty("bar").objectReferenceValue = bar.transform;
        controllerData.FindProperty("bodyData").objectReferenceValue = bodyData;
        controllerData.FindProperty("deductionData").objectReferenceValue = deductionData;
        controllerData.FindProperty("settings.pumpStrength").floatValue = 1.6f;  // tuned in play
        controllerData.FindProperty("settings.groundY").floatValue = 0f;
        controllerData.ApplyModifiedProperties();

        SetReference(GetOrAdd<RoundFlow>(bar), "gymnast", controller);
        WireView(GetOrAdd<GymnastView>(gymnastObject), controller, gymnast);

        var hud = Object.FindAnyObjectByType<SwingHUD>();
        if (hud != null) SetReference(hud, "gymnast", controller);

        var steel = GetMaterial("Bar", new Color(0.78f, 0.80f, 0.83f), 0.9f, 0.8f);
        var barVisual = FindOrCreate("BarVisual", PrimitiveType.Cylinder, null);
        Place(barVisual, new Vector3(0f, BarHeight, 0f), Quaternion.Euler(90f, 0f, 0f),
            new Vector3(BarDiameter, BarLength / 2f, BarDiameter));  // cylinder mesh is 2 m long along Y
        SetMaterial(barVisual, steel);

        var apparatus = FindOrCreate("Apparatus", null, null);
        Place(apparatus, Vector3.zero, Quaternion.identity, Vector3.one);
        foreach (float z in new[] { -BarLength / 2f, BarLength / 2f })
        {
            string name = z < 0f ? "UprightNear" : "UprightFar";
            var upright = FindOrCreate(name, PrimitiveType.Cylinder, apparatus.transform);
            Place(upright, new Vector3(0f, BarHeight / 2f, z), Quaternion.identity,
                new Vector3(0.05f, BarHeight / 2f, 0.05f));
            SetMaterial(upright, steel);
        }

        var oldMat = GameObject.Find("Ground");
        if (oldMat != null)
        {
            Undo.RecordObject(oldMat, "Rename Ground");
            oldMat.name = "LandingMat";
        }
        var mat = FindOrCreate("LandingMat", PrimitiveType.Cube, null);
        Place(mat, new Vector3(0f, -MatThickness / 2f, 0f), Quaternion.identity, new Vector3(10f, MatThickness, 4f));
        SetMaterial(mat, GetMaterial("Mat", new Color(0.18f, 0.35f, 0.70f), 0f, 0.2f));

        var floor = FindOrCreate("Floor", PrimitiveType.Plane, null);
        Place(floor, new Vector3(0f, -MatThickness, 0f), Quaternion.identity, new Vector3(6f, 1f, 6f));  // plane mesh is 10 m
        SetMaterial(floor, GetMaterial("Floor", new Color(0.25f, 0.25f, 0.27f), 0f, 0.1f));

        SetUpCamera(bar.transform, gymnast);
        SetUpLight();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(bar.scene);
        Debug.Log("3D scene built. Save the scene with File > Save.");
    }

    /// <summary>
    /// Builds the bone hierarchy under the Gymnast object. Limb bones point down their local -Y,
    /// so GymnastView bends them with a local Z rotation. Each bone has a primitive as its visual.
    /// </summary>
    private static void BuildFigure(GameObject gymnast, BodyModel body)
    {
        // The old capsule figure kept its mesh on the Gymnast object itself.
        foreach (var component in new Component[]
                 { gymnast.GetComponent<MeshRenderer>(), gymnast.GetComponent<MeshFilter>(), gymnast.GetComponent<Collider>() })
        {
            if (component != null) Undo.DestroyObjectImmediate(component);
        }
        var oldHips = gymnast.transform.Find("Hips");
        if (oldHips != null) Undo.DestroyObjectImmediate(oldHips.gameObject);

        var singlet = GetMaterial("Gymnast", new Color(0.80f, 0.12f, 0.15f), 0f, 0.4f);
        var pants = GetMaterial("Pants", new Color(0.92f, 0.92f, 0.94f), 0f, 0.3f);
        var skin = GetMaterial("Skin", new Color(0.87f, 0.68f, 0.55f), 0f, 0.3f);
        var stripe = GetMaterial("Stripe", new Color(1f, 0.85f, 0.2f), 0f, 0.5f);

        var hips = Bone("Hips", gymnast.transform, Vector3.zero);
        var spine = Bone("Spine", hips, Vector3.zero);
        float trunkDepth = 2f * body.TrunkRadius * TrunkDepthFactor;
        float trunkWidth = 2f * body.TrunkRadius * TrunkWidthFactor;
        Visual(PrimitiveType.Capsule, spine, new Vector3(0f, body.TrunkLength / 2f, 0f),
            new Vector3(trunkDepth, body.TrunkLength / 2f, trunkWidth), singlet);
        // A stripe across the chest shows which way the gymnast faces, so the twist is visible.
        Visual(PrimitiveType.Cube, spine, new Vector3(trunkDepth * 0.45f, body.TrunkLength * 0.65f, 0f),
            new Vector3(0.03f, 0.07f, trunkWidth * 0.8f), stripe);
        var head = Bone("Head", spine, new Vector3(0f, body.TrunkLength, 0f));
        Visual(PrimitiveType.Sphere, head, new Vector3(0f, body.HeadLength * 0.6f, 0f), Vector3.one * 2f * body.HeadRadius, skin);

        foreach (var side in new[] { "Left", "Right" })
        {
            float z = side == "Left" ? 1f : -1f;

            var upperArm = Bone(side + "UpperArm", spine, new Vector3(0f, body.TrunkLength, z * body.ShoulderHalfWidth));
            Limb(upperArm, body.UpperArmLength, 2f * body.UpperArmRadius, singlet);
            var lowerArm = Bone(side + "LowerArm", upperArm, new Vector3(0f, -body.UpperArmLength, 0f));
            Limb(lowerArm, body.ForearmLength, 2f * body.ForearmRadius, skin);
            var hand = Bone(side + "Hand", lowerArm, new Vector3(0f, -body.ForearmLength, 0f));
            Visual(PrimitiveType.Sphere, hand, new Vector3(0f, -body.HandLength / 2f, 0f), Vector3.one * 2f * body.HandRadius, skin);

            var upperLeg = Bone(side + "UpperLeg", hips, new Vector3(0f, 0f, z * body.HipHalfWidth));
            Limb(upperLeg, body.ThighLength, 2f * body.ThighRadius, pants);
            var lowerLeg = Bone(side + "LowerLeg", upperLeg, new Vector3(0f, -body.ThighLength, 0f));
            Limb(lowerLeg, body.ShankLength, 2f * body.ShankRadius, pants);
            var foot = Bone(side + "Foot", lowerLeg, new Vector3(0f, -body.ShankLength, 0f));
            Visual(PrimitiveType.Sphere, foot, new Vector3(0f, -body.FootLength / 2f, 0f), Vector3.one * 2f * body.FootRadius, skin);
        }
    }

    private static void WireView(GymnastView view, GymnastController controller, Transform gymnast)
    {
        var serialized = new SerializedObject(view);
        serialized.FindProperty("controller").objectReferenceValue = controller;
        foreach (var bone in new[]
                 {
                     "Hips", "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm",
                     "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg"
                 })
        {
            string field = char.ToLowerInvariant(bone[0]) + bone.Substring(1);   // "LeftUpperArm" -> "leftUpperArm"
            serialized.FindProperty(field).objectReferenceValue = FindDeep(gymnast, bone);
        }
        serialized.ApplyModifiedProperties();
    }

    private static Transform Bone(string name, Transform parent, Vector3 localPosition)
    {
        var bone = new GameObject(name).transform;
        Undo.RegisterCreatedObjectUndo(bone.gameObject, name);
        bone.SetParent(parent, false);
        bone.localPosition = localPosition;
        return bone;
    }

    // A capsule from the bone's joint down its local -Y, as long as the segment.
    private static void Limb(Transform bone, float length, float thickness, Material material) =>
        Visual(PrimitiveType.Capsule, bone, new Vector3(0f, -length / 2f, 0f),
            new Vector3(thickness, length / 2f, thickness), material);   // capsule mesh is 2 m tall

    private static void Visual(PrimitiveType type, Transform bone, Vector3 localPosition, Vector3 scale, Material material)
    {
        var visual = GameObject.CreatePrimitive(type);
        visual.name = bone.name + "Mesh";
        Object.DestroyImmediate(visual.GetComponent<Collider>());   // physics is hand-written
        visual.transform.SetParent(bone, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = scale;
        visual.GetComponent<Renderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(visual, visual.name);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetUpCamera(Transform bar, Transform gymnast)
    {
        var camera = Camera.main;
        Undo.RecordObject(camera, "Camera");
        camera.orthographic = false;
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        var follow = GetOrAdd<CameraFollow>(camera.gameObject);
        SetReference(follow, "target", gymnast);
        SetReference(follow, "anchor", bar);
    }

    private static void SetUpLight()
    {
        var light = Object.FindAnyObjectByType<Light>();
        if (light == null || light.type != LightType.Directional) return;
        Undo.RecordObject(light.transform, "Light");
        Undo.RecordObject(light, "Light");
        light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        light.shadows = LightShadows.Soft;
    }

    private static GameObject Require(string name)
    {
        var found = GameObject.Find(name);
        if (found == null) Debug.LogError($"No '{name}' object in the open scene.");
        return found;
    }

    // Finds a scene object by name or creates it (a primitive, or an empty object when type is null).
    private static GameObject FindOrCreate(string name, PrimitiveType? type, Transform parent)
    {
        var found = GameObject.Find(name);
        if (found != null) return found;

        var created = type.HasValue ? GameObject.CreatePrimitive(type.Value) : new GameObject();
        created.name = name;
        if (type.HasValue) Object.DestroyImmediate(created.GetComponent<Collider>());  // physics is hand-written
        created.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(created, name);
        return created;
    }

    private static void Place(GameObject target, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Undo.RecordObject(target.transform, target.name);
        target.transform.SetPositionAndRotation(position, rotation);
        target.transform.localScale = scale;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void SetReference(Object component, string field, Object value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void SetMaterial(GameObject target, Material material)
    {
        var renderer = target.GetComponent<Renderer>();
        Undo.RecordObject(renderer, target.name);
        renderer.sharedMaterial = material;
    }

    // Loads a URP Lit material from Assets/Materials, creating it the first time.
    private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
