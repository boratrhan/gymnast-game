using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu that turns the open scene into the real-scale 3D high bar scene:
/// mat top at y = 0, bar 2.6 m above it (bar axis along Z), perspective camera that follows the gymnast,
/// and the gameplay components (GymnastController, GymnastView, RoundFlow) wired together.
/// Safe to run again: objects are found by name and updated, missing ones are created.
/// </summary>
public static class GymnastSceneBuilder
{
    private const float BarHeight = 2.6f;        // above the top of the mat
    private const float BarLength = 2.4f;
    private const float BarDiameter = 0.028f;
    private const float CentreOfMassRadius = 1.1f;   // bar to the gymnast's centre of mass when hanging
    private const float BodyHeight = 1.7f;
    private const float BodyThickness = 0.35f;
    private const float MatThickness = 0.2f;
    private const string MaterialFolder = "Assets/Materials";

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

        var gymnast = gymnastObject.transform;
        if (gymnast.parent != null) Undo.SetTransformParent(gymnast, null, "Gymnast");
        Place(gymnastObject, new Vector3(0f, BarHeight - CentreOfMassRadius, 0f), Quaternion.identity,
            new Vector3(BodyThickness, BodyHeight / 2f, BodyThickness));  // capsule mesh is 2 m tall
        SetMaterial(gymnastObject, GetMaterial("Gymnast", new Color(0.80f, 0.12f, 0.15f), 0f, 0.4f));

        var controller = GetOrAdd<GymnastController>(bar);
        // Inspector values are stored in the scene, so changing the defaults in code is not enough.
        var controllerData = new SerializedObject(controller);
        controllerData.FindProperty("bar").objectReferenceValue = bar.transform;
        controllerData.FindProperty("settings.armLength").floatValue = CentreOfMassRadius;
        controllerData.FindProperty("settings.pumpStrength").floatValue = 1.6f;  // tuned in play for the 1.1 m radius
        controllerData.FindProperty("settings.groundY").floatValue = 0f;
        controllerData.FindProperty("settings.halfHeight").floatValue = BodyHeight / 2f;
        controllerData.FindProperty("settings.bodyRadius").floatValue = BodyThickness / 2f;
        controllerData.ApplyModifiedProperties();

        SetReference(GetOrAdd<RoundFlow>(bar), "gymnast", controller);
        var view = GetOrAdd<GymnastView>(gymnastObject);
        SetReference(view, "controller", controller);
        SetReference(view, "body", gymnast);

        var hud = Object.FindFirstObjectByType<SwingHUD>();
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
        var light = Object.FindFirstObjectByType<Light>();
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
