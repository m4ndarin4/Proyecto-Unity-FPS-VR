using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

public static class VRSetupTools
{
    const string ScenePath = "Assets/Scenes/MiEscenarioFPS.unity";
    const string VRScenePath = "Assets/Scenes/MiEscenarioVR.unity";

    static GameObject FindPrefab(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    [MenuItem("Tools/VR/Configurar OpenXR")]
    public static void ConfigureXR()
    {
        var perTarget = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
            .Select(g => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        if (perTarget == null)
        {
            Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        foreach (var grp in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
        {
            if (!perTarget.HasSettingsForBuildTarget(grp)) perTarget.CreateDefaultSettingsForBuildTarget(grp);
            if (!perTarget.HasManagerSettingsForBuildTarget(grp)) perTarget.CreateDefaultManagerSettingsForBuildTarget(grp);
            var gs = perTarget.SettingsForBuildTarget(grp);
            gs.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(gs.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", grp);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[VR] OpenXR asignado (PC y Android)");
    }

    [MenuItem("Tools/VR/Crear escena VR")]
    public static void BuildVRScene()
    {
        // Copia de la escena FPS -> escena VR
        if (!File.Exists(VRScenePath)) AssetDatabase.CopyAsset(ScenePath, VRScenePath);
        var scene = EditorSceneManager.OpenScene(VRScenePath);

        Vector3 pos = new Vector3(37.5f, 8f, 37.5f);
        var old = GameObject.Find("Jugador");
        if (old != null) { pos = old.transform.position; Object.DestroyImmediate(old); }
        var terrain = Object.FindObjectOfType<Terrain>();
        if (terrain != null) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.05f;

        foreach (var c in Object.FindObjectsOfType<Camera>()) Object.DestroyImmediate(c.gameObject);

        var setup = FindPrefab("XR Interaction Setup");
        if (setup == null) { Debug.LogError("[VR] No se encontró 'XR Interaction Setup' (importar Starter Assets)"); return; }
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(setup);
        rig.transform.position = pos;

        var sim = FindPrefab("XR Device Simulator");
        if (sim != null) PrefabUtility.InstantiatePrefab(sim);
        else Debug.LogWarning("[VR] No se encontró XR Device Simulator");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(VRScenePath, true) };
        Debug.Log("[VR] Escena VR lista en " + pos);
    }

    public static void BatchAll()
    {
        ConfigureXR();
        BuildVRScene();
    }
}
