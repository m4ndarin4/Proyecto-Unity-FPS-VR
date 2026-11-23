using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FPSSetupTools
{
    const string ScenePath = "Assets/Scenes/MiEscenarioFPS.unity";

    [MenuItem("Tools/FPS/1. Crear Jugador en la escena")]
    public static void SetupPlayer()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var player = GameObject.Find("Jugador");
        if (player == null)
        {
            player = new GameObject("Jugador");
            Vector3 pos = Vector3.zero;
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                Vector3 size = terrain.terrainData.size;
                pos = terrain.transform.position + new Vector3(size.x * 0.5f, 0, size.z * 0.5f);
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 1.2f;
            }
            player.transform.position = pos;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;
            player.AddComponent<FPSController>();

            // Cuerpo visible del muñeco
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Cuerpo";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(player.transform, false);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Mat_Azul.mat");
            if (mat != null) body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var cam = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 0.8f, 0);
            cam.transform.localRotation = Quaternion.identity;
            cam.GetComponent<Camera>().nearClipPlane = 0.1f;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("[FPS] Jugador listo en " + player.transform.position);
    }

    [MenuItem("Tools/FPS/2. Borrar assets no usados")]
    public static void CleanUnused()
    {
        var keep = new HashSet<string>(AssetDatabase.GetDependencies(ScenePath, true));
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p))
            .Where(p => !p.StartsWith("Assets/Scripts/") && !p.StartsWith("Assets/Editor/"))
            .Where(p => !keep.Contains(p)).ToList();
        foreach (var p in all) { Debug.Log("[FPS] Borrando " + p); AssetDatabase.DeleteAsset(p); }
        // borrar carpetas vacías
        foreach (var dir in Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (Directory.GetFiles(dir).All(f => f.EndsWith(".meta")) && Directory.GetDirectories(dir).Length == 0)
                AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));
        }
        AssetDatabase.Refresh();
        Debug.Log("[FPS] Limpieza terminada. Borrados: " + all.Count);
    }

    // Renderiza capturas: vista en tercera persona del muñeco recorriendo el terreno
    public static void RenderPreview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var player = GameObject.Find("Jugador");
        var terrain = Object.FindObjectOfType<Terrain>();
        Directory.CreateDirectory("Preview");
        var camGo = new GameObject("PreviewCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60;
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        Vector3 start = player.transform.position;
        for (int i = 0; i < 6; i++)
        {
            // simula caminar hacia adelante con giros
            float ang = i * 30f;
            Vector3 p = start + Quaternion.Euler(0, ang, 0) * Vector3.forward * (i * 6f);
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 1.0f;
            player.transform.SetPositionAndRotation(p, Quaternion.Euler(0, ang, 0));
            camGo.transform.position = p - player.transform.forward * 6f + Vector3.up * 3f;
            camGo.transform.LookAt(p + Vector3.up * 0.5f);
            Save(cam, rt, "Preview/tercera_persona_" + i + ".png");
            var fps = player.GetComponentInChildren<Camera>();
            if (fps != null && fps != cam) { fps.targetTexture = rt; Save(fps, rt, "Preview/primera_persona_" + i + ".png"); fps.targetTexture = null; }
        }
        // vista aérea
        if (terrain != null)
        {
            var s = terrain.terrainData.size;
            camGo.transform.position = terrain.transform.position + new Vector3(s.x / 2, Mathf.Max(s.x, s.z) * 0.6f, -s.z * 0.1f);
            camGo.transform.LookAt(terrain.transform.position + new Vector3(s.x / 2, 0, s.z / 2));
            Save(cam, rt, "Preview/vista_aerea.png");
        }
        Debug.Log("[FPS] Previews generados");
    }

    static void Save(Camera cam, RenderTexture rt, string path)
    {
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
    }

    [MenuItem("Tools/FPS/3. Agregar colisiones a los autos")]
    public static void AddCarColliders()
    {
        int n = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (!go.name.StartsWith("Car") && !go.name.StartsWith("Bus") && !go.name.StartsWith("Taxi") && !go.name.StartsWith("Police")) continue;
            if (go.transform.parent != null) continue;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() != null) continue;
                Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
                n++;
            }
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[FPS] Colliders agregados a autos: " + n + ". Guarda la escena (Ctrl+S).");
    }

    [MenuItem("Tools/FPS/4. Modificar terreno (crear colina)")]
    public static void ModifyTerrain()
    {
        var terrain = Object.FindObjectOfType<Terrain>();
        if (terrain == null) { Debug.LogError("[FPS] No hay Terrain"); return; }
        var td = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Modificar terreno");
        Vector3 tp = terrain.transform.position, size = td.size;

        // Objetos a evitar (casas, autos, jugador, rocas, letreros)
        var avoid = Object.FindObjectsOfType<Transform>()
            .Where(t => t.parent == null && t.GetComponent<Terrain>() == null && t.GetComponent<Light>() == null)
            .Select(t => t.position).ToList();

        // Buscar el punto (zona central 20%-80%) más lejano a los objetos
        Vector2 best = new Vector2(0.5f, 0.5f); float bestD = -1;
        for (float u = 0.2f; u <= 0.8f; u += 0.02f)
            for (float v = 0.2f; v <= 0.8f; v += 0.02f)
            {
                Vector3 w = tp + new Vector3(u * size.x, 0, v * size.z);
                float d = avoid.Count == 0 ? 1 : avoid.Min(a => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(w.x, w.z)));
                if (d > bestD) { bestD = d; best = new Vector2(u, v); }
            }

        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        float radius = Mathf.Clamp(bestD * 0.8f, 4f, size.x * 0.15f); // metros
        float altura = 6f / size.y; // colina de ~6 m
        int cx = Mathf.RoundToInt(best.x * (res - 1)), cy = Mathf.RoundToInt(best.y * (res - 1));
        int r = Mathf.CeilToInt(radius / size.x * (res - 1));
        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(res - 1, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(res - 1, cx + r); x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d >= 1f) continue;
                float k = 0.5f * (1f + Mathf.Cos(d * Mathf.PI)); // forma suave
                h[y, x] += altura * k;
            }
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FPS] Colina creada en ({tp.x + best.x * size.x:F1}, {tp.z + best.y * size.z:F1}), radio {radius:F1} m. Ctrl+Z para deshacer.");
    }

    public static void BatchTerrain()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        AddCarColliders();
        ModifyTerrain();
        EditorSceneManager.SaveScene(scene);
        RenderPreview();
    }

    public static void BatchAll()
    {
        SetupPlayer();
        CleanUnused();
        RenderPreview();
    }
}
