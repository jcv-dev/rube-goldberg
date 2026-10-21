using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the complete Rube Goldberg scene from scratch.
/// Menu: Tools > Build Rube Goldberg Machine
/// </summary>
public static class RubeGoldbergBuilder
{
    private const string ScenePath = "Assets/Scenes/RubeGoldberg.unity";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Materials";
    private const string TextureFolder = "Assets/Textures";
    private const float LaneLength = 14f;
    private const float LaneSlope = 5.0f;
    private const int StageTotal = 16;

    private sealed class MaterialSet
    {
        public Material Ground;
        public Material Wood;
        public Material WoodDark;
        public Material Brass;
        public Material DarkMetal;
        public Material Domino;
        public Material Marble;
        public Material Ember;
        public Material Teal;
        public Material Chute;
        public Material Trail;
    }

    private sealed class PrefabSet
    {
        public GameObject Domino;
        public GameObject Marble;
        public GameObject Gate;
        public GameObject PaddleWheel;
    }

    private sealed class Lane
    {
        public GameObject Root;
        public Transform Transform;
        public bool DownRight;

        public Vector3 Point(float x, float y, float z)
        {
            return Transform.TransformPoint(new Vector3(x, y, z));
        }
    }

    private static Transform s_root;
    private static Transform s_structure;
    private static Transform s_laneGroup;
    private static Transform s_moduleGroup;
    private static Transform s_lighting;
    private static MaterialSet s_mat;
    private static PrefabSet s_prefab;
    private static PhysicsMaterial s_lowFriction;
    private static PhysicsMaterial s_dominoFriction;
    private static MachineHud s_hud;
    private static Font s_font;
    private static int s_stageIndex;
    private static readonly List<MachineStageTrigger> s_stageTriggers = new();

    [MenuItem("Tools/Build Rube Goldberg Machine", false, 1)]
    public static void BuildMachine()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        try
        {
            Debug.Log("[Rube Goldberg] Building scene...");
            EnsureFolders();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMaterialSet();
            s_prefab = BuildPrefabs();

            s_root = new GameObject("Rube Goldberg Machine").transform;
            s_structure = NewGroup("00 - Stage & Structure", s_root);
            s_laneGroup = NewGroup("01 - Marble Runs", s_root);
            s_moduleGroup = NewGroup("02 - Mechanisms", s_root);
            s_lighting = NewGroup("03 - Lighting", s_root);

            ConfigureEnvironment();
            BuildStructure();
            BuildLighting();
            BuildPostProcessing();
            BuildHud();

            s_stageIndex = 0;
            s_stageTriggers.Clear();

            Lane lane1 = BuildLane("Lane 01 - Upper Run", 7.6f, true);
            Lane lane2 = BuildLane("Lane 02 - Return Run", 4.95f, false, 5.2f, 2.0f, 1.95f);
            Lane lane3 = BuildLane("Lane 03 - Final Run", 2.7f, true, 0f, 0f, 1.95f);

            GameObject marble = InstantiatePrefab(
                s_prefab.Marble,
                lane1.Transform,
                new Vector3(-6.8f, 2.05f, 0f),
                Quaternion.identity);
            marble.name = "Main Marble";

            BuildStartTower(lane1);
            AddLaneStage(lane1, "Gravity release", new Vector3(-6.8f, 0.85f, 0f), new Vector3(1.55f, 2.3f, 1.45f));

            BuildLaneOne(lane1, lane2);
            BuildLaneTwo(lane2, lane3);
            BuildLaneThree(lane3);

            BuildCameras(marble.transform);

            if (s_stageTriggers.Count != StageTotal)
            {
                Debug.LogWarning($"[Rube Goldberg] Expected {StageTotal} stage triggers, found {s_stageTriggers.Count}.");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();

            int rigidbodies = s_root.GetComponentsInChildren<Rigidbody>(true).Length;
            int colliders = s_root.GetComponentsInChildren<Collider>(true).Length;
            Debug.Log($"[Rube Goldberg] Scene built: {s_stageTriggers.Count} stages, {rigidbodies} rigidbodies, {colliders} colliders. Saved to {ScenePath}.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = s_root.gameObject;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Rube Goldberg] Build failed: {exception}");
            throw;
        }
    }

    [MenuItem("Tools/Rube Goldberg/Open Machine Scene", false, 20)]
    public static void OpenMachineScene()
    {
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[Rube Goldberg] Build the scene first with Tools > Build Rube Goldberg Machine.");
        }
    }

    // ---------------------------------------------------------------------
    // Folders, assets and materials
    // ---------------------------------------------------------------------

    private static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/Editor",
            "Assets/Scripts",
            "Assets/Materials",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Textures",
            "Assets/Screenshots"
        };

        foreach (string folder in folders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                continue;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(Path.GetFullPath(folder));
                AssetDatabase.Refresh();
            }
        }
    }

    private static void BuildMaterialSet()
    {
        Texture2D wood = CreateWoodTexture();
        Texture2D woodNormal = CreateWoodNormal();
        Texture2D metal = CreateBrushedMetalTexture();
        Texture2D metalNormal = CreateBrushedMetalNormal();
        Texture2D ground = CreateGroundTexture();
        Texture2D groundNormal = CreateGroundNormal();
        Texture2D domino = CreateDominoTexture();
        Texture2D dominoEmission = CreateDominoEmission();
        Texture2D marble = CreateMarbleTexture();
        Texture2D marbleNormal = CreateMarbleNormal();
        Texture2D marbleEmission = CreateMarbleEmission();

        s_lowFriction = CreatePhysicsMaterial("LowFriction", 0.04f, 0.04f, 0.04f);
        s_dominoFriction = CreatePhysicsMaterial("DominoFriction", 0.45f, 0.65f, 0f);

        s_mat = new MaterialSet
        {
            Ground = CreateLitMaterial("Ground", ground, groundNormal, Color.white, 0.05f, 0.18f, null, Color.black, new Vector2(2f, 2f)),
            Wood = CreateLitMaterial("Walnut", wood, woodNormal, new Color(0.92f, 0.82f, 0.70f), 0.02f, 0.34f, null, Color.black, new Vector2(3f, 1f)),
            WoodDark = CreateLitMaterial("Walnut Dark", wood, woodNormal, new Color(0.42f, 0.32f, 0.26f), 0.05f, 0.28f, null, Color.black, new Vector2(2.5f, 1f)),
            Brass = CreateLitMaterial("Brass", metal, metalNormal, new Color(0.93f, 0.67f, 0.29f), 0.82f, 0.72f, null, Color.black, new Vector2(1f, 2f)),
            DarkMetal = CreateLitMaterial("Dark Metal", metal, metalNormal, new Color(0.10f, 0.13f, 0.16f), 0.72f, 0.48f, null, Color.black, new Vector2(1f, 1.5f)),
            Domino = CreateLitMaterial("Domino", domino, null, Color.white, 0.02f, 0.52f, dominoEmission, new Color(1.5f, 0.52f, 0.22f), new Vector2(1f, 1f), TextureWrapMode.Clamp),
            Marble = CreateLitMaterial("Sun Marble", marble, marbleNormal, Color.white, 0.30f, 0.84f, marbleEmission, new Color(3.2f, 1.25f, 0.35f), new Vector2(1f, 1f), TextureWrapMode.Repeat),
            Ember = CreateLitMaterial("Ember Glow", null, null, new Color(1f, 0.30f, 0.06f), 0.1f, 0.4f, null, new Color(3.5f, 0.7f, 0.13f)),
            Teal = CreateLitMaterial("Teal Glow", null, null, new Color(0.05f, 0.42f, 0.46f), 0.1f, 0.35f, null, new Color(0.12f, 1.8f, 1.9f)),
            Chute = CreateLitMaterial("Chute Brass", metal, metalNormal, new Color(0.80f, 0.52f, 0.22f), 0.7f, 0.65f, null, Color.black, new Vector2(1f, 1f)),
            Trail = CreateTrailMaterial()
        };
    }

    private static PhysicsMaterial CreatePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness)
    {
        string path = $"{MaterialFolder}/{name}.physicMaterial";
        AssetDatabase.DeleteAsset(path);
        var material = new PhysicsMaterial(name)
        {
            dynamicFriction = dynamicFriction,
            staticFriction = staticFriction,
            bounciness = bounciness,
            frictionCombine = dynamicFriction > 0.5f ? PhysicsMaterialCombine.Maximum : PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material CreateLitMaterial(
        string name,
        Texture2D baseMap,
        Texture2D normalMap,
        Color baseColor,
        float metallic,
        float smoothness,
        Texture2D emissionMap,
        Color emissionColor,
        Vector2? tiling = null,
        TextureWrapMode wrap = TextureWrapMode.Repeat)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        string path = $"{MaterialFolder}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        var material = new Material(shader) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", baseColor);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);

        if (baseMap != null)
        {
            baseMap.wrapMode = wrap;
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
        }

        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        if (emissionMap != null)
        {
            material.SetTexture("_EmissionMap", emissionMap);
        }

        if (emissionColor.maxColorComponent > 0f)
        {
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (tiling.HasValue)
        {
            material.SetTextureScale("_BaseMap", tiling.Value);
            material.SetTextureScale("_MainTex", tiling.Value);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material CreateTrailMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        string path = $"{MaterialFolder}/Marble Trail.mat";
        AssetDatabase.DeleteAsset(path);
        var material = new Material(shader) { name = "Marble Trail" };
        material.SetColor("_Color", new Color(1f, 0.55f, 0.14f, 0.8f));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ---------------------------------------------------------------------
    // Procedural textures
    // ---------------------------------------------------------------------

    private static Texture2D CreateWoodTexture()
    {
        const int size = 512;
        Color[] pixels = new Color[size * size];
        float[] heights = new float[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float warp = Mathf.PerlinNoise(x * 0.019f, y * 0.006f) * 1.7f;
                float grain = Mathf.Sin((x * 0.11f) + warp) * 0.5f + 0.5f;
                grain = Mathf.Pow(grain, 1.8f);
                float fine = Mathf.PerlinNoise(x * 0.20f, y * 0.028f);
                float value = grain * 0.7f + fine * 0.3f;
                Color dark = new Color(0.105f, 0.052f, 0.028f);
                Color light = new Color(0.48f, 0.25f, 0.105f);
                pixels[y * size + x] = Color.Lerp(dark, light, value);
                heights[y * size + x] = value * 0.65f + fine * 0.25f;
            }
        }

        return SaveTexture("Walnut_Base", size, size, pixels, false, TextureWrapMode.Repeat);
    }

    private static Texture2D CreateWoodNormal()
    {
        const int size = 512;
        float[] height = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float warp = Mathf.PerlinNoise(x * 0.019f, y * 0.006f) * 1.7f;
                float grain = Mathf.Sin((x * 0.11f) + warp) * 0.5f + 0.5f;
                grain = Mathf.Pow(grain, 1.8f);
                height[y * size + x] = grain;
            }
        }

        return SaveNormalTexture("Walnut_Normal", size, size, height, 1.7f);
    }

    private static Texture2D CreateBrushedMetalTexture()
    {
        const int size = 512;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float streak = Mathf.PerlinNoise(x * 0.008f, y * 0.42f);
                float sparkle = Mathf.PerlinNoise(x * 0.22f, y * 0.15f);
                float value = 0.22f + streak * 0.38f + sparkle * 0.04f;
                pixels[y * size + x] = new Color(value, value * 0.97f, value * 0.92f);
            }
        }

        return SaveTexture("BrushedMetal_Base", size, size, pixels, false, TextureWrapMode.Repeat);
    }

    private static Texture2D CreateBrushedMetalNormal()
    {
        const int size = 512;
        float[] height = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                height[y * size + x] = Mathf.PerlinNoise(x * 0.02f, y * 0.5f);
            }
        }

        return SaveNormalTexture("BrushedMetal_Normal", size, size, height, 0.7f);
    }

    private static Texture2D CreateGroundTexture()
    {
        const int size = 512;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.035f, y * 0.035f);
                float grid = (x % 128 < 2 || y % 128 < 2) ? 0.03f : 0f;
                float value = 0.075f + noise * 0.075f + grid;
                pixels[y * size + x] = new Color(value * 0.84f, value * 0.91f, value);
            }
        }

        return SaveTexture("StudioFloor_Base", size, size, pixels, false, TextureWrapMode.Repeat);
    }

    private static Texture2D CreateGroundNormal()
    {
        const int size = 512;
        float[] height = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                height[y * size + x] = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.35f;
            }
        }

        return SaveNormalTexture("StudioFloor_Normal", size, size, height, 0.55f);
    }

    private static Texture2D CreateDominoTexture()
    {
        const int width = 256;
        const int height = 512;
        Color ivory = new Color(0.95f, 0.89f, 0.78f);
        Color ink = new Color(0.16f, 0.12f, 0.10f);
        Color coral = new Color(0.93f, 0.35f, 0.20f);
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = ivory;
        }

        DrawRect(pixels, width, height, 0, 0, width, 22, coral);
        DrawRect(pixels, width, height, 0, height - 22, width, height, coral);
        DrawRect(pixels, width, height, 10, 10, 16, height - 10, coral);
        DrawRect(pixels, width, height, width - 16, 10, width - 10, height - 10, coral);
        DrawRect(pixels, width, height, 22, height / 2 - 3, width - 22, height / 2 + 3, ink);
        DrawPip(pixels, width, height, 0.32f, 0.80f, 13, ink);
        DrawPip(pixels, width, height, 0.68f, 0.80f, 13, ink);
        DrawPip(pixels, width, height, 0.32f, 0.20f, 13, ink);
        DrawPip(pixels, width, height, 0.68f, 0.20f, 13, ink);
        DrawPip(pixels, width, height, 0.50f, 0.50f, 10, coral);

        return SaveTexture("Domino_Base", width, height, pixels, false, TextureWrapMode.Clamp);
    }

    private static Texture2D CreateDominoEmission()
    {
        const int width = 256;
        const int height = 512;
        Color[] pixels = new Color[width * height];
        Color clear = Color.black;
        Color glow = new Color(0.95f, 0.35f, 0.12f);

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }

        DrawPip(pixels, width, height, 0.32f, 0.80f, 9, glow);
        DrawPip(pixels, width, height, 0.68f, 0.80f, 9, glow);
        DrawPip(pixels, width, height, 0.32f, 0.20f, 9, glow);
        DrawPip(pixels, width, height, 0.68f, 0.20f, 9, glow);
        DrawRect(pixels, width, height, 10, 10, 16, height - 10, glow * 0.55f);
        DrawRect(pixels, width, height, width - 16, 10, width - 10, height - 10, glow * 0.55f);

        return SaveTexture("Domino_Emission", width, height, pixels, false, TextureWrapMode.Clamp);
    }

    private static Texture2D CreateMarbleTexture()
    {
        const int width = 512;
        const int height = 256;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float bands = Mathf.Sin((y * 0.18f + Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 14f)) * 0.5f + 0.5f;
                float grain = Mathf.PerlinNoise(x * 0.09f, y * 0.16f);
                Color deep = new Color(0.16f, 0.035f, 0.022f);
                Color amber = new Color(0.98f, 0.42f, 0.07f);
                Color pale = new Color(1f, 0.82f, 0.36f);
                Color color = Color.Lerp(deep, amber, bands * 0.8f + grain * 0.2f);
                if (Mathf.Abs(y - height * 0.5f) < 2f)
                {
                    color = pale;
                }
                pixels[y * width + x] = color;
            }
        }

        return SaveTexture("SunMarble_Base", width, height, pixels, false, TextureWrapMode.Repeat);
    }

    private static Texture2D CreateMarbleNormal()
    {
        const int width = 512;
        const int height = 256;
        float[] heights = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                heights[y * width + x] = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.7f +
                                        Mathf.Sin(x * 0.22f) * 0.10f;
            }
        }

        return SaveNormalTexture("SunMarble_Normal", width, height, heights, 0.7f);
    }

    private static Texture2D CreateMarbleEmission()
    {
        const int width = 512;
        const int height = 256;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float equator = Mathf.Exp(-Mathf.Pow((y - height * 0.5f) / 2.3f, 2f));
                float stripe = Mathf.Exp(-Mathf.Pow((y - height * 0.21f) / 3.2f, 2f)) * 0.45f;
                float value = Mathf.Clamp01(equator + stripe);
                pixels[y * width + x] = new Color(1f, 0.48f, 0.11f) * value;
            }
        }

        return SaveTexture("SunMarble_Emission", width, height, pixels, false, TextureWrapMode.Repeat);
    }

    private static Texture2D SaveTexture(string name, int width, int height, Color[] pixels, bool normalMap, TextureWrapMode wrap)
    {
        string path = $"{TextureFolder}/{name}.png";
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? TextureFolder);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap;
        importer.wrapMode = wrap;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D SaveNormalTexture(string name, int width, int height, float[] heightMap, float strength)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float left = heightMap[y * width + Mathf.Max(0, x - 1)];
                float right = heightMap[y * width + Mathf.Min(width - 1, x + 1)];
                float down = heightMap[Mathf.Max(0, y - 1) * width + x];
                float up = heightMap[Mathf.Min(height - 1, y + 1) * width + x];
                Vector3 normal = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                pixels[y * width + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f);
            }
        }

        return SaveTexture(name, width, height, pixels, true, TextureWrapMode.Repeat);
    }

    private static void DrawRect(Color[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color color)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(height, y1); y++)
        {
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(width, x1); x++)
            {
                pixels[y * width + x] = color;
            }
        }
    }

    private static void DrawPip(Color[] pixels, int width, int height, float u, float v, int radius, Color color)
    {
        int cx = Mathf.RoundToInt(u * width);
        int cy = Mathf.RoundToInt(v * height);
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    continue;
                }

                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                {
                    pixels[y * width + x] = color;
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Prefabs
    // ---------------------------------------------------------------------

    private static PrefabSet BuildPrefabs()
    {
        return new PrefabSet
        {
            Domino = BuildDominoPrefab(),
            Marble = BuildMarblePrefab(),
            Gate = BuildGatePrefab(),
            PaddleWheel = BuildPaddleWheelPrefab()
        };
    }

    private static GameObject BuildDominoPrefab()
    {
        GameObject domino = GameObject.CreatePrimitive(PrimitiveType.Cube);
        domino.name = "Domino";
        domino.transform.localScale = new Vector3(0.16f, 0.62f, 0.42f);
        domino.GetComponent<Renderer>().sharedMaterial = s_mat.Domino;
        domino.GetComponent<Collider>().sharedMaterial = s_dominoFriction;

        Rigidbody body = domino.AddComponent<Rigidbody>();
        body.mass = 0.08f;
        body.linearDamping = 0.06f;
        body.angularDamping = 0.04f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        domino.AddComponent<DominoClearance>();

        return SavePrefab(domino, $"{PrefabFolder}/Domino.prefab");
    }

    private static GameObject BuildMarblePrefab()
    {
        GameObject marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marble.name = "Marble";
        marble.tag = "Player";
        marble.transform.localScale = Vector3.one * 0.72f;
        marble.GetComponent<Renderer>().sharedMaterial = s_mat.Marble;

        Rigidbody body = marble.AddComponent<Rigidbody>();
        body.mass = 4.2f;
        body.linearDamping = 0.015f;
        body.angularDamping = 0.04f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.maxAngularVelocity = 60f;
        body.sleepThreshold = 0.005f;
        marble.AddComponent<MarbleFlowAssist>();

        SphereCollider sphere = marble.GetComponent<SphereCollider>();
        sphere.sharedMaterial = s_lowFriction;

        TrailRenderer trail = marble.AddComponent<TrailRenderer>();
        trail.material = s_mat.Trail;
        trail.time = 0.55f;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = 0.13f;
        trail.endWidth = 0.01f;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.colorGradient = MakeTrailGradient();

        GameObject glow = new GameObject("Amber Glow");
        glow.transform.SetParent(marble.transform, false);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.50f, 0.16f);
        light.intensity = 1.8f;
        light.range = 2.4f;
        light.shadows = LightShadows.None;

        return SavePrefab(marble, $"{PrefabFolder}/Marble.prefab");
    }

    private static Gradient MakeTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.25f), 0f),
                new GradientColorKey(new Color(1f, 0.27f, 0.05f), 0.55f),
                new GradientColorKey(new Color(0.2f, 0.03f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.75f, 0f),
                new GradientAlphaKey(0.25f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static GameObject BuildGatePrefab()
    {
        var gate = new GameObject("Swing Gate");
        Rigidbody body = gate.AddComponent<Rigidbody>();
        CompoundPassThrough gatePass = gate.AddComponent<CompoundPassThrough>();
        gatePass.contactWindow = 0.035f;
        body.mass = 0.42f;
        body.linearDamping = 0.05f;
        body.angularDamping = 0.10f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        HingeJoint hinge = gate.AddComponent<HingeJoint>();
        hinge.axis = Vector3.up;
        hinge.anchor = new Vector3(0f, 0.55f, 0f);
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -100f, max = 100f, bounciness = 0f };
        hinge.enablePreprocessing = true;

        GameObject panel = Box(
            "Gate Panel",
            gate.transform,
            new Vector3(0f, 0.55f, -0.45f),
            new Vector3(0.075f, 1.1f, 0.9f),
            Quaternion.identity,
            s_mat.WoodDark,
            true);
        Box(
            "Gate Brass Edge",
            panel.transform,
            new Vector3(0f, 0f, -0.43f),
            new Vector3(1.15f, 0.95f, 0.04f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        var handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = "Gate Handle";
        handle.transform.SetParent(gate.transform, false);
        handle.transform.localPosition = new Vector3(0f, 0.62f, -0.82f);
        handle.transform.localScale = Vector3.one * 0.12f;
        handle.GetComponent<Renderer>().sharedMaterial = s_mat.Brass;
        UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());

        return SavePrefab(gate, $"{PrefabFolder}/SwingGate.prefab");
    }

    private static GameObject BuildPaddleWheelPrefab()
    {
        var wheel = new GameObject("Paddle Wheel");
        Rigidbody body = wheel.AddComponent<Rigidbody>();
        CompoundPassThrough wheelPass = wheel.AddComponent<CompoundPassThrough>();
        wheelPass.contactWindow = 0.045f;
        body.mass = 0.26f;
        body.linearDamping = 0.04f;
        body.angularDamping = 0.16f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        HingeJoint hinge = wheel.AddComponent<HingeJoint>();
        hinge.axis = Vector3.forward;
        hinge.anchor = Vector3.zero;
        hinge.enablePreprocessing = true;

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            Quaternion spin = Quaternion.Euler(0f, 0f, angle);
            GameObject blade = Box(
                $"Paddle {i + 1}",
                wheel.transform,
                spin * new Vector3(0f, 0.37f, 0f),
                new Vector3(0.11f, 0.72f, 0.62f),
                spin,
                s_mat.Brass,
                true);
            Box(
                $"Paddle Tip {i + 1}",
                blade.transform,
                new Vector3(0f, 0.49f, 0f),
                new Vector3(1.25f, 0.08f, 0.62f),
                Quaternion.identity,
                s_mat.Teal,
                false);
        }

        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "Hub";
        hub.transform.SetParent(wheel.transform, false);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hub.transform.localScale = new Vector3(0.20f, 0.075f, 0.20f);
        hub.GetComponent<Renderer>().sharedMaterial = s_mat.DarkMetal;
        UnityEngine.Object.DestroyImmediate(hub.GetComponent<Collider>());

        return SavePrefab(wheel, $"{PrefabFolder}/PaddleWheel.prefab");
    }

    private static GameObject SavePrefab(GameObject source, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path, out bool success);
        UnityEngine.Object.DestroyImmediate(source);
        if (!success)
        {
            throw new InvalidOperationException($"Could not save prefab at {path}.");
        }

        return prefab;
    }

    // ---------------------------------------------------------------------
    // Scene structure
    // ---------------------------------------------------------------------

    private static void ConfigureEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.20f, 0.26f, 0.34f);
        RenderSettings.ambientEquatorColor = new Color(0.16f, 0.16f, 0.19f);
        RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.045f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.011f;
        RenderSettings.fogColor = new Color(0.018f, 0.026f, 0.035f);
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.skybox = null;
    }

    private static void BuildStructure()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Studio Floor";
        floor.transform.SetParent(s_structure, false);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(4.2f, 1f, 4.2f);
        floor.GetComponent<Renderer>().sharedMaterial = s_mat.Ground;

        GameObject backdrop = Box(
            "Backdrop",
            s_structure,
            new Vector3(0f, 5.4f, 3.6f),
            new Vector3(25f, 12f, 0.24f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
        backdrop.GetComponent<Renderer>().sharedMaterial = s_mat.Ground;

        Box(
            "Walnut Plinth",
            s_structure,
            new Vector3(0f, 0.55f, 0.25f),
            new Vector3(18.6f, 1.1f, 4.5f),
            Quaternion.identity,
            s_mat.Wood,
            true);

        Box(
            "Plinth Brass Lip",
            s_structure,
            new Vector3(0f, 1.105f, -1.88f),
            new Vector3(18.7f, 0.035f, 0.055f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        float[] xs = { -8.8f, 8.8f };
        float[] zs = { -1.25f, 1.25f };
        foreach (float x in xs)
        {
            foreach (float z in zs)
            {
                Box(
                    "Frame Column",
                    s_structure,
                    new Vector3(x, 5.15f, z),
                    new Vector3(0.25f, 8.1f, 0.25f),
                    Quaternion.identity,
                    s_mat.DarkMetal,
                    false);
            }
        }

        foreach (float z in zs)
        {
            Box(
                "Top Beam",
                s_structure,
                new Vector3(0f, 9.22f, z),
                new Vector3(17.85f, 0.25f, 0.25f),
                Quaternion.identity,
                s_mat.DarkMetal,
                false);
        }

        Box(
            "Back Crossbar Lower",
            s_structure,
            new Vector3(0f, 3.65f, 1.25f),
            new Vector3(17.7f, 0.16f, 0.2f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        Box(
            "Back Crossbar Upper",
            s_structure,
            new Vector3(0f, 6.35f, 1.25f),
            new Vector3(17.7f, 0.16f, 0.2f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        foreach (float x in new[] { -8.62f, 8.62f })
        {
            Box(
                "Light Inlay",
                s_structure,
                new Vector3(x, 5.2f, -1.38f),
                new Vector3(0.035f, 7.3f, 0.035f),
                Quaternion.identity,
                s_mat.Ember,
                false);
        }

        // Thin verticals behind the runs help the structure read as a real machine.
        foreach (float x in new[] { -6.2f, -2.1f, 2.1f, 6.2f })
        {
            Box(
                "Lane Support",
                s_structure,
                new Vector3(x, 3.6f, 0.72f),
                new Vector3(0.075f, 4.9f, 0.075f),
                Quaternion.identity,
                s_mat.Brass,
                false);
        }

        // Soft light bars behind the sculpture.
        foreach (float x in new[] { -7.5f, 7.5f })
        {
            Box(
                "Backdrop Light Bar",
                s_structure,
                new Vector3(x, 5.2f, 3.46f),
                new Vector3(0.07f, 6.1f, 0.05f),
                Quaternion.identity,
                s_mat.Teal,
                false);
        }
    }

    private static void BuildLighting()
    {
        GameObject sun = new GameObject("Key Light");
        sun.transform.SetParent(s_lighting, false);
        sun.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.90f, 0.78f);
        sunLight.intensity = 1.85f;
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = 0.75f;

        CreateSpot(
            "Warm Key Spot",
            new Vector3(-5.2f, 10.8f, -5.6f),
            new Vector3(0f, 5.0f, 0f),
            new Color(1f, 0.72f, 0.48f),
            1500f,
            60f,
            26f,
            LightShadows.Soft);

        CreateSpot(
            "Cool Fill",
            new Vector3(7.5f, 8.7f, 3.2f),
            new Vector3(0f, 4.6f, 0f),
            new Color(0.36f, 0.70f, 1f),
            1150f,
            66f,
            24f,
            LightShadows.None);

        CreateSpot(
            "Amber Rim",
            new Vector3(5.6f, 7.1f, 4.4f),
            new Vector3(2.5f, 5.2f, 0f),
            new Color(1f, 0.36f, 0.14f),
            850f,
            50f,
            19f,
            LightShadows.None);

        CreateSpot(
            "Front Fill",
            new Vector3(0f, 7.4f, -8.6f),
            new Vector3(0f, 5.0f, 0f),
            new Color(0.88f, 0.93f, 1f),
            720f,
            74f,
            26f,
            LightShadows.None);

        CreatePoint(
            "Warm Bounce",
            new Vector3(-4.5f, 2.1f, -3.2f),
            new Color(1f, 0.40f, 0.16f),
            48f,
            8f);

        CreatePoint(
            "Cool Bounce",
            new Vector3(5.5f, 2f, -2.8f),
            new Color(0.12f, 0.55f, 0.75f),
            42f,
            8f);

        CreatePoint(
            "Front Softbox",
            new Vector3(0f, 6.2f, -5.8f),
            new Color(0.82f, 0.88f, 1f),
            70f,
            18f);
    }

    private static void CreateSpot(string name, Vector3 position, Vector3 target, Color color, float intensity, float angle, float range, LightShadows shadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(s_lighting, false);
        go.transform.position = position;
        go.transform.LookAt(target);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.55f;
        light.shadows = shadows;
        light.shadowStrength = 0.7f;
    }

    private static void CreatePoint(string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(s_lighting, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static void BuildPostProcessing()
    {
        string profilePath = $"{MaterialFolder}/GoldenCircuit_Profile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        Bloom bloom = GetOrAddOverride<Bloom>(profile);
        bloom.active = true;
        bloom.intensity.Override(0.55f);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.62f);
        bloom.tint.Override(new Color(1f, 0.82f, 0.62f));

        Tonemapping tonemapping = GetOrAddOverride<Tonemapping>(profile);
        tonemapping.active = true;
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = GetOrAddOverride<ColorAdjustments>(profile);
        color.active = true;
        color.postExposure.Override(0.35f);
        color.contrast.Override(8f);
        color.saturation.Override(5f);
        color.colorFilter.Override(new Color(1f, 0.96f, 0.90f));

        Vignette vignette = GetOrAddOverride<Vignette>(profile);
        vignette.active = true;
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.48f);
        vignette.color.Override(new Color(0.01f, 0.012f, 0.018f));

        FilmGrain grain = GetOrAddOverride<FilmGrain>(profile);
        grain.active = true;
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.10f);
        grain.response.Override(0.7f);

        AssetDatabase.SaveAssets();

        var volumeGO = new GameObject("Global Volume");
        volumeGO.transform.SetParent(s_lighting, false);
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
        {
            return component;
        }

        component = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void BuildHud()
    {
        s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_font == null)
        {
            s_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        var canvasGO = new GameObject("Interface");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        MachineHud hud = canvasGO.AddComponent<MachineHud>();
        hud.totalStages = StageTotal;

        Color panelColor = new Color(0.025f, 0.045f, 0.06f, 0.82f);
        Color gold = new Color(0.91f, 0.68f, 0.35f);
        Color ivory = new Color(0.96f, 0.93f, 0.87f);
        Color muted = new Color(0.58f, 0.64f, 0.68f);

        RectTransform topPanel = AddUiPanel(
            "Title Plate",
            canvas.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(56f, -54f),
            new Vector2(720f, 136f),
            panelColor);
        AddUiImage(
            "Title Accent",
            topPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(6f, 136f),
            gold);

        Text title = AddUiText(
            "Title",
            topPanel,
            "THE GOLDEN CIRCUIT",
            34,
            ivory,
            TextAnchor.UpperLeft,
            new Vector2(28f, -18f),
            new Vector2(660f, 42f),
            FontStyle.Bold);
        AddUiText(
            "Subtitle",
            topPanel,
            "RUBE GOLDBERG MACHINE   /   " + StageTotal + " PHYSICS INTERACTIONS   /   UNITY 6.6",
            16,
            muted,
            TextAnchor.UpperLeft,
            new Vector2(28f, -70f),
            new Vector2(660f, 28f),
            FontStyle.Normal);

        RectTransform stagePlate = AddUiPanel(
            "Stage Counter Plate",
            canvas.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-56f, -54f),
            new Vector2(315f, 136f),
            panelColor);
        AddUiText(
            "Stage Caption",
            stagePlate,
            "STAGE",
            14,
            muted,
            TextAnchor.UpperRight,
            new Vector2(20f, -22f),
            new Vector2(275f, 24f),
            FontStyle.Bold);
        Text counter = AddUiText(
            "Stage Counter",
            stagePlate,
            $"00  /  {StageTotal:00}",
            42,
            gold,
            TextAnchor.LowerRight,
            new Vector2(20f, -42f),
            new Vector2(275f, 58f),
            FontStyle.Bold);

        RectTransform bottomPlate = AddUiPanel(
            "Stage Description Plate",
            canvas.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(56f, 56f),
            new Vector2(760f, 118f),
            panelColor);
        AddUiImage(
            "Bottom Accent",
            bottomPlate,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(6f, 118f),
            gold);

        Text stageName = AddUiText(
            "Stage Name",
            bottomPlate,
            "AWAITING RELEASE",
            27,
            ivory,
            TextAnchor.UpperLeft,
            new Vector2(28f, -18f),
            new Vector2(690f, 38f),
            FontStyle.Bold);
        Text status = AddUiText(
            "Status",
            bottomPlate,
            "RUBE GOLDBERG MACHINE / LIVE PHYSICS",
            14,
            muted,
            TextAnchor.UpperLeft,
            new Vector2(28f, -62f),
            new Vector2(690f, 22f),
            FontStyle.Normal);

        RectTransform progressTrack = AddUiPanel(
            "Progress Track",
            bottomPlate,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(28f, 18f),
            new Vector2(460f, 3f),
            new Color(0.18f, 0.24f, 0.27f, 0.9f));
        Image fill = AddUiImage(
            "Progress Fill",
            progressTrack,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            Vector2.zero,
            new Vector2(460f, 3f),
            gold);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        AddUiText(
            "Footer",
            canvas.transform,
            "REAL-TIME 3D PHYSICS   /   BUILT-IN EDITOR GENERATOR",
            12,
            new Color(muted.r, muted.g, muted.b, 0.72f),
            TextAnchor.LowerRight,
            new Vector2(-56f, 58f),
            new Vector2(600f, 20f),
            FontStyle.Normal,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f));

        hud.stageCounter = counter;
        hud.stageName = stageName;
        hud.status = status;
        hud.progressFill = fill;
        s_hud = hud;
    }

    private static RectTransform AddUiPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Image AddUiImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddUiText(
        string name,
        Transform parent,
        string content,
        int size,
        Color color,
        TextAnchor anchor,
        Vector2 position,
        Vector2 dimensions,
        FontStyle style,
        Vector2? customAnchorMin = null,
        Vector2? customAnchorMax = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Vector2 anchorMin = customAnchorMin ?? new Vector2(0f, 1f);
        Vector2 anchorMax = customAnchorMax ?? new Vector2(0f, 1f);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        Text text = go.GetComponent<Text>();
        text.font = s_font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // ---------------------------------------------------------------------
    // Lanes, prefab instances and shared props
    // ---------------------------------------------------------------------

    private static Lane BuildLane(string name, float centerY, bool downRight, float gapCenter = 0f, float gapLength = 0f, float highExtension = 0f)
    {
        var lane = new GameObject(name);
        lane.transform.SetParent(s_laneGroup, false);
        lane.transform.position = new Vector3(0f, centerY, 0f);
        lane.transform.rotation = Quaternion.Euler(0f, 0f, downRight ? -LaneSlope : LaneSlope);

        float deckLength = LaneLength + highExtension;
        float deckShift = (downRight ? -1f : 1f) * highExtension * 0.5f;

        if (gapLength > 0.01f)
        {
            float gapStart = gapCenter - gapLength * 0.5f;
            float gapEnd = gapCenter + gapLength * 0.5f;
            float leftStart = -deckLength * 0.5f + deckShift;
            float rightEnd = deckLength * 0.5f + deckShift;
            float leftLength = gapStart - leftStart;
            float rightLength = rightEnd - gapEnd;
            Box(
                "Walnut Deck High",
                lane.transform,
                new Vector3(gapEnd + rightLength * 0.5f, -0.09f, 0f),
                new Vector3(rightLength, 0.18f, 1f),
                Quaternion.identity,
                s_mat.Wood,
                true);
            Box(
                "Walnut Deck Low",
                lane.transform,
                new Vector3(leftStart + leftLength * 0.5f, -0.09f, 0f),
                new Vector3(leftLength, 0.18f, 1f),
                Quaternion.identity,
                s_mat.Wood,
                true);
        }
        else
        {
            Box(
                "Walnut Deck",
                lane.transform,
                new Vector3(deckShift, -0.09f, 0f),
                new Vector3(deckLength, 0.18f, 1f),
                Quaternion.identity,
                s_mat.Wood,
                true);
        }

        Box(
            "Brass Rail Left",
            lane.transform,
            new Vector3(deckShift, 0.105f, -0.57f),
            new Vector3(deckLength, 0.24f, 0.075f),
            Quaternion.identity,
            s_mat.Brass,
            true);

        Box(
            "Brass Rail Right",
            lane.transform,
            new Vector3(deckShift, 0.105f, 0.57f),
            new Vector3(deckLength, 0.24f, 0.075f),
            Quaternion.identity,
            s_mat.Brass,
            true);

        Box(
            "Under Beam",
            lane.transform,
            new Vector3(deckShift, -0.32f, 0f),
            new Vector3(deckLength, 0.20f, 0.44f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);

        for (float x = -5.8f; x <= 5.9f; x += 2.9f)
        {
            Box(
                "Deck Bracket",
                lane.transform,
                new Vector3(x, -0.45f, 0f),
                new Vector3(0.16f, 0.36f, 1.18f),
                Quaternion.identity,
                s_mat.Brass,
                false);
        }

        return new Lane { Root = lane, Transform = lane.transform, DownRight = downRight };
    }

    private static void AddLandingTray(Lane lane, int endSign)
    {
        float centerX = endSign * 7.58f;
        Box(
            "Drop Backstop",
            lane.Transform,
            new Vector3(endSign * 8.72f, 1.05f, 0f),
            new Vector3(0.11f, 2.4f, 1.32f),
            Quaternion.identity,
            s_mat.DarkMetal,
            true);
        Box(
            "Drop Backstop Glow",
            lane.Transform,
            new Vector3(endSign * 8.66f, 1.05f, 0f),
            new Vector3(0.025f, 2.25f, 0.035f),
            Quaternion.identity,
            s_mat.Ember,
            false);
        Box(
            "Drop Guard Left",
            lane.Transform,
            new Vector3(centerX, 0.55f, -0.66f),
            new Vector3(1.95f, 0.95f, 0.06f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
        Box(
            "Drop Guard Right",
            lane.Transform,
            new Vector3(centerX, 0.55f, 0.66f),
            new Vector3(1.95f, 0.95f, 0.06f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        return instance;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool withCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!withCollider)
        {
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool withCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!withCollider)
        {
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go;
    }

    private static void AddLaneStage(Lane lane, string label, Vector3 localPosition, Vector3 size)
    {
        AddStage(label, lane.Transform, localPosition, size, false);
    }

    private static void AddWorldStage(string label, Vector3 worldPosition, Vector3 size)
    {
        AddStage(label, s_root, worldPosition, size, true);
    }

    private static void AddStage(string label, Transform parent, Vector3 position, Vector3 size, bool worldPosition)
    {
        s_stageIndex++;
        var go = new GameObject($"Stage {s_stageIndex:00} - {label}");
        go.transform.SetParent(parent, false);
        if (worldPosition)
        {
            go.transform.position = position;
        }
        else
        {
            go.transform.localPosition = position;
        }

        go.transform.localRotation = Quaternion.identity;
        BoxCollider trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = size;
        MachineStageTrigger stage = go.AddComponent<MachineStageTrigger>();
        stage.hud = s_hud;
        stage.stageIndex = s_stageIndex;
        stage.stageLabel = label;
        s_stageTriggers.Add(stage);
    }

    // ---------------------------------------------------------------------
    // Upper run
    // ---------------------------------------------------------------------

    private static void BuildStartTower(Lane lane)
    {
        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                float px = -6.8f + (x == 0 ? -0.56f : 0.56f);
                float pz = z == 0 ? -0.53f : 0.53f;
                Box(
                    "Start Tower Post",
                    lane.Transform,
                    new Vector3(px, 1.08f, pz),
                    new Vector3(0.055f, 2.05f, 0.055f),
                    Quaternion.identity,
                    s_mat.Brass,
                    true);
            }
        }

        Box(
            "Start Tower Crown",
            lane.Transform,
            new Vector3(-6.8f, 2.03f, 0f),
            new Vector3(1.34f, 0.075f, 1.25f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);

        Box(
            "Start Tower Ember",
            lane.Transform,
            new Vector3(-6.8f, 2.08f, -0.61f),
            new Vector3(1.1f, 0.018f, 0.025f),
            Quaternion.identity,
            s_mat.Ember,
            false);

        Box(
            "Start Plate",
            lane.Transform,
            new Vector3(-6.8f, 0.04f, 0f),
            new Vector3(0.95f, 0.06f, 0.88f),
            Quaternion.identity,
            s_mat.Brass,
            false);
    }

    private static void BuildLaneOne(Lane lane, Lane lane2)
    {
        AddDominoRun(lane, "Domino Run I", -5.65f, 4, 0.44f);
        AddLaneStage(lane, "Domino cascade I", new Vector3(-5.35f, 0.72f, 0f), new Vector3(1.2f, 1.7f, 1.3f));

        BuildPaddleWheel(lane, -2.45f, "Paddle Wheel I");
        AddLaneStage(lane, "Paddle wheel", new Vector3(-2.45f, 0.82f, 0f), new Vector3(0.85f, 1.85f, 1.35f));

        BuildGate(lane, -0.9f, "Swing Gate I");
        AddLaneStage(lane, "Swing gate", new Vector3(-0.9f, 0.72f, 0f), new Vector3(0.72f, 1.65f, 1.4f));

        BuildChimes(lane, 0.8f);
        AddLaneStage(lane, "Copper chimes", new Vector3(0.98f, 0.78f, 0.20f), new Vector3(1.55f, 1.6f, 1.25f));

        AddDominoRun(lane, "Domino Run II", 2.15f, 4, 0.44f);
        AddLaneStage(lane, "Domino cascade II", new Vector3(2.4f, 0.72f, 0f), new Vector3(1.2f, 1.7f, 1.3f));

        AddLandingTray(lane2, 1);
        Vector3 upperEnd = lane.Point(LaneLength * 0.5f, 0f, 0f);
        Vector3 landingPoint = lane2.Point(7.45f, 0f, 0f);
        Vector3 funnelTop = new Vector3(landingPoint.x, upperEnd.y - 0.12f, 0f);
        BuildFunnel("Funnel Drop I", funnelTop, 2.00f, 1.80f, 0.45f);
        AddWorldStage("Brass funnel drop", funnelTop + new Vector3(0f, -0.22f, 0f), new Vector3(2.2f, 1.4f, 2.2f));
    }

    private static void BuildLaneTwo(Lane lane, Lane lane3)
    {
        BuildSeesaw(lane, 5.2f);
        AddLaneStage(lane, "See-saw bridge", new Vector3(5.2f, 0.72f, 0f), new Vector3(1.45f, 1.55f, 1.35f));

        BuildPendulum(lane, 3.35f);
        AddLaneStage(lane, "Pendulum", new Vector3(3.35f, 0.95f, 0f), new Vector3(0.85f, 1.75f, 1.35f));

        BuildBumpers(lane, 1.85f, 5);
        AddLaneStage(lane, "Bumper alley", new Vector3(1.35f, 0.55f, 0f), new Vector3(1.8f, 1.1f, 1.35f));

        AddDominoRun(lane, "Domino Run III", -1.75f, 4, -0.44f);
        AddLaneStage(lane, "Domino cascade III", new Vector3(-1.35f, 0.72f, 0f), new Vector3(1.2f, 1.7f, 1.3f));

        AddLandingTray(lane3, -1);
        Vector3 upperEnd = lane.Point(-LaneLength * 0.5f, 0f, 0f);
        Vector3 landingPoint = lane3.Point(-7.45f, 0f, 0f);
        Vector3 funnelTop = new Vector3(landingPoint.x, upperEnd.y - 0.12f, 0f);
        BuildFunnel("Funnel Drop II", funnelTop, 2.00f, 1.80f, 0.45f);
        AddWorldStage("Second funnel drop", funnelTop + new Vector3(0f, -0.22f, 0f), new Vector3(2.2f, 1.4f, 2.2f));
    }

    private static void BuildLaneThree(Lane lane)
    {
        BuildGate(lane, -5.35f, "Swing Gate II");
        AddLaneStage(lane, "Swing gate II", new Vector3(-5.35f, 0.72f, 0f), new Vector3(0.72f, 1.65f, 1.4f));

        BuildPaddleWheel(lane, -3.6f, "Paddle Wheel II");
        AddLaneStage(lane, "Paddle wheel II", new Vector3(-3.6f, 0.82f, 0f), new Vector3(0.85f, 1.85f, 1.35f));

        AddDominoRun(lane, "Domino Run IV", -1.85f, 4, 0.44f);
        AddLaneStage(lane, "Final dominoes", new Vector3(-1.55f, 0.72f, 0f), new Vector3(1.2f, 1.7f, 1.3f));

        BuildHump(lane, 2.8f);
        BuildFinishGong(lane, 5.9f);
        AddLaneStage(lane, "Victory bell", new Vector3(5.55f, 0.95f, 0f), new Vector3(0.95f, 1.8f, 1.35f));
    }

    private static void AddDominoRun(Lane lane, string name, float startX, int count, float step)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject domino = InstantiatePrefab(
                s_prefab.Domino,
                lane.Transform,
                new Vector3(startX + i * step, 0.325f, 0f),
                Quaternion.identity);
            domino.name = $"{name} {i + 1:00}";
            domino.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // A small brass number strip gives each cascade a visual anchor.
        float mid = startX + (count - 1) * step * 0.5f;
        Box(
            name + " Marker",
            lane.Transform,
            new Vector3(mid, 0.012f, 0.47f),
            new Vector3(Mathf.Abs(step) * (count - 1) + 0.5f, 0.018f, 0.03f),
            Quaternion.identity,
            s_mat.Ember,
            false);
    }

    private static void BuildPaddleWheel(Lane lane, float x, string name)
    {
        GameObject wheel = InstantiatePrefab(
            s_prefab.PaddleWheel,
            lane.Transform,
            new Vector3(x, 1.30f, 0f),
            Quaternion.identity);
        wheel.name = name;

        Cylinder(
            "Paddle Wheel Post",
            lane.Transform,
            new Vector3(x, 1.08f, 0.74f),
            new Vector3(0.07f, 1.08f, 0.07f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
        Cylinder(
            "Paddle Wheel Axle",
            lane.Transform,
            new Vector3(x, 1.30f, 0.37f),
            new Vector3(0.045f, 0.37f, 0.045f),
            Quaternion.Euler(90f, 0f, 0f),
            s_mat.Brass,
            false);
        Box(
            "Paddle Wheel Bearing",
            lane.Transform,
            new Vector3(x, 1.30f, 0.74f),
            new Vector3(0.18f, 0.18f, 0.06f),
            Quaternion.identity,
            s_mat.Brass,
            false);
    }

    private static void BuildGate(Lane lane, float x, string name)
    {
        // The hinge is intentionally slightly outside the lane so the panel
        // can swing without colliding with the rails.
        GameObject gate = InstantiatePrefab(
            s_prefab.Gate,
            lane.Transform,
            new Vector3(x, 0.02f, 0.48f),
            Quaternion.identity);
        gate.name = name;

        Cylinder(
            "Gate Pivot",
            lane.Transform,
            new Vector3(x, 0.57f, 0.48f),
            new Vector3(0.055f, 0.61f, 0.055f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        Box(
            "Gate Header",
            lane.Transform,
            new Vector3(x, 1.16f, 0.01f),
            new Vector3(0.10f, 0.07f, 1.23f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
    }

    private static void BuildChimes(Lane lane, float x)
    {
        Box(
            "Chime Rail",
            lane.Transform,
            new Vector3(x + 0.30f, 2.02f, 0.35f),
            new Vector3(1.7f, 0.07f, 0.07f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);

        for (int i = 0; i < 3; i++)
        {
            float cx = x + i * 0.34f;
            GameObject root = new GameObject($"Chime {i + 1}");
            root.transform.SetParent(lane.Transform, false);
            root.transform.localPosition = new Vector3(cx, 1.18f, 0.35f);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.07f;
            body.linearDamping = 0.03f;
            body.angularDamping = 0.04f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            HingeJoint hinge = root.AddComponent<HingeJoint>();
            hinge.axis = Vector3.forward;
            hinge.anchor = Vector3.zero;
            hinge.useLimits = true;
            hinge.limits = new JointLimits { min = -40f, max = 40f };

            GameObject tube = Cylinder(
                "Brass Tube",
                root.transform,
                new Vector3(0f, -0.49f, 0f),
                new Vector3(0.085f, 0.44f, 0.085f),
                Quaternion.identity,
                s_mat.Brass,
                true);
            tube.GetComponent<Collider>().sharedMaterial = s_lowFriction;
            Box(
                "Chime Tie",
                root.transform,
                new Vector3(0f, -0.03f, 0f),
                new Vector3(0.20f, 0.035f, 0.20f),
                Quaternion.identity,
                s_mat.Ember,
                false);
        }
    }

    private static void BuildSeesaw(Lane lane, float x)
    {
        // The deck has a matching 2 m opening here, so the hinged plank sits
        // perfectly flush with the run. The marble rolls on, tips the bridge,
        // and exits without ever meeting a raised lip.
        var pivot = new GameObject("See-saw Bridge");
        pivot.transform.SetParent(s_moduleGroup, false);
        pivot.transform.position = lane.Point(x, 0f, 0f);
        pivot.transform.rotation = lane.Transform.rotation;
        Rigidbody body = pivot.AddComponent<Rigidbody>();
        body.mass = 0.9f;
        body.linearDamping = 0.10f;
        body.angularDamping = 0.35f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        HingeJoint hinge = pivot.AddComponent<HingeJoint>();
        hinge.axis = Vector3.forward;
        hinge.anchor = Vector3.zero;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -8f, max = 8f, bounciness = 0f };

        GameObject plank = Box(
            "See-saw Plank",
            pivot.transform,
            new Vector3(0f, -0.015f, 0f),
            new Vector3(2.0f, 0.03f, 0.86f),
            Quaternion.identity,
            s_mat.WoodDark,
            true);
        MarblePassThrough pass = plank.AddComponent<MarblePassThrough>();
        pass.contactWindow = 0.08f;
        Box(
            "See-saw Brass Strip",
            pivot.transform,
            new Vector3(0f, 0.001f, 0f),
            new Vector3(2.0f, 0.008f, 0.08f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        // Hidden safety deck: after the plank has reacted, the marble drops
        // two centimetres onto this continuous surface and keeps moving.
        Box(
            "See-saw Safety Deck",
            lane.Transform,
            new Vector3(x, -0.12f, 0f),
            new Vector3(2.05f, 0.16f, 0.86f),
            Quaternion.identity,
            s_mat.WoodDark,
            true);

        Cylinder(
            "See-saw Fulcrum",
            lane.Transform,
            new Vector3(x, -0.42f, 0f),
            new Vector3(0.16f, 0.20f, 0.16f),
            Quaternion.Euler(90f, 0f, 0f),
            s_mat.DarkMetal,
            false);
        Box(
            "See-saw Axle",
            lane.Transform,
            new Vector3(x, 0f, 0f),
            new Vector3(0.09f, 0.09f, 1.06f),
            Quaternion.identity,
            s_mat.Brass,
            false);
    }

    private static void BuildPendulum(Lane lane, float x)
    {
        Box(
            "Pendulum Mount",
            lane.Transform,
            new Vector3(x, 2.02f, 0f),
            new Vector3(0.85f, 0.08f, 0.11f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
        Cylinder(
            "Pendulum Post",
            lane.Transform,
            new Vector3(x, 1.08f, 0.46f),
            new Vector3(0.045f, 1.02f, 0.045f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        var root = new GameObject("Pendulum Bob");
        root.transform.SetParent(s_moduleGroup, false);
        root.transform.position = lane.Point(x, 1.96f, 0f);
        root.transform.rotation = lane.Transform.rotation;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.50f;
        body.linearDamping = 0.02f;
        body.angularDamping = 0.03f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.automaticCenterOfMass = false;
        body.centerOfMass = new Vector3(0f, -1.2f, 0f);
        HingeJoint hinge = root.AddComponent<HingeJoint>();
        hinge.axis = Vector3.forward;
        hinge.anchor = Vector3.zero;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -70f, max = 70f, bounciness = 0.15f };

        Cylinder(
            "Pendulum Rod",
            root.transform,
            new Vector3(0f, -0.69f, 0f),
            new Vector3(0.025f, 0.70f, 0.025f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        GameObject bob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bob.name = "Pendulum Weight";
        bob.transform.SetParent(root.transform, false);
        bob.transform.localPosition = new Vector3(0f, -1.38f, 0f);
        bob.transform.localScale = Vector3.one * 0.43f;
        bob.GetComponent<Renderer>().sharedMaterial = s_mat.Brass;
        bob.GetComponent<SphereCollider>().sharedMaterial = s_lowFriction;
        bob.AddComponent<MarblePassThrough>();
    }

    private static void BuildBumpers(Lane lane, float startX, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = startX - i * 0.36f;
            float z = (i % 2 == 0) ? 0.24f : -0.24f;
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = $"Bumper Dome {i + 1:00}";
            dome.transform.SetParent(lane.Transform, false);
            dome.transform.localPosition = new Vector3(x, 0.02f, z);
            dome.transform.localScale = new Vector3(0.24f, 0.09f, 0.24f);
            dome.GetComponent<Renderer>().sharedMaterial = s_mat.Brass;
            UnityEngine.Object.DestroyImmediate(dome.GetComponent<Collider>());
            BoxCollider domeCollider = dome.AddComponent<BoxCollider>();
            domeCollider.size = Vector3.one;
            domeCollider.sharedMaterial = s_lowFriction;
            MarblePassThrough pass = dome.AddComponent<MarblePassThrough>();
            pass.contactWindow = 0.04f;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Bumper Glow";
            cap.transform.SetParent(dome.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            cap.transform.localScale = new Vector3(0.52f, 0.24f, 0.52f);
            cap.GetComponent<Renderer>().sharedMaterial = s_mat.Teal;
            UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
        }
    }

    private static void BuildHump(Lane lane, float x)
    {
        Cylinder(
            "Final Crest",
            lane.Transform,
            new Vector3(x, 0.015f, 0f),
            new Vector3(0.20f, 0.05f, 0.20f),
            Quaternion.Euler(90f, 0f, 0f),
            s_mat.Brass,
            false);
        Box(
            "Crest Glow",
            lane.Transform,
            new Vector3(x, 0.045f, 0f),
            new Vector3(0.025f, 0.012f, 0.84f),
            Quaternion.identity,
            s_mat.Ember,
            false);
    }

    private static void BuildFinishGong(Lane lane, float x)
    {
        Box(
            "Gong Post",
            lane.Transform,
            new Vector3(x + 0.42f, 1.20f, 0.72f),
            new Vector3(0.10f, 2.35f, 0.10f),
            Quaternion.identity,
            s_mat.DarkMetal,
            false);
        Box(
            "Gong Arm",
            lane.Transform,
            new Vector3(x + 0.16f, 2.32f, 0.36f),
            new Vector3(0.70f, 0.09f, 0.10f),
            Quaternion.identity,
            s_mat.Brass,
            false);
        Box(
            "Finish Arch Left",
            lane.Transform,
            new Vector3(x, 1.18f, -0.66f),
            new Vector3(0.09f, 2.3f, 0.09f),
            Quaternion.identity,
            s_mat.Brass,
            false);
        Box(
            "Finish Arch Right",
            lane.Transform,
            new Vector3(x, 1.18f, 0.66f),
            new Vector3(0.09f, 2.3f, 0.09f),
            Quaternion.identity,
            s_mat.Brass,
            false);
        Box(
            "Finish Arch Top",
            lane.Transform,
            new Vector3(x, 2.30f, 0f),
            new Vector3(0.10f, 0.09f, 1.42f),
            Quaternion.identity,
            s_mat.Ember,
            false);

        var gong = new GameObject("Victory Gong");
        gong.transform.SetParent(s_moduleGroup, false);
        gong.transform.position = lane.Point(x, 1.83f, 0f);
        gong.transform.rotation = lane.Transform.rotation;
        Rigidbody body = gong.AddComponent<Rigidbody>();
        body.mass = 1.4f;
        body.linearDamping = 0.03f;
        body.angularDamping = 0.08f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        HingeJoint hinge = gong.AddComponent<HingeJoint>();
        hinge.axis = Vector3.forward;
        hinge.anchor = Vector3.zero;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -28f, max = 28f, bounciness = 0.2f };

        Cylinder(
            "Gong Rod",
            gong.transform,
            new Vector3(0f, -0.26f, 0f),
            new Vector3(0.025f, 0.27f, 0.025f),
            Quaternion.identity,
            s_mat.Brass,
            false);

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Gong Disc";
        disc.transform.SetParent(gong.transform, false);
        disc.transform.localPosition = new Vector3(0f, -0.82f, 0f);
        disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        disc.transform.localScale = new Vector3(0.96f, 0.05f, 0.96f);
        disc.GetComponent<Renderer>().sharedMaterial = s_mat.Brass;
        UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
        BoxCollider collider = disc.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 2f, 1f);
        collider.sharedMaterial = s_lowFriction;

        Cylinder(
            "Gong Boss",
            gong.transform,
            new Vector3(-0.08f, -0.82f, 0f),
            new Vector3(0.23f, 0.04f, 0.23f),
            Quaternion.Euler(0f, 0f, 90f),
            s_mat.Ember,
            false);
    }

    // ---------------------------------------------------------------------
    // Funnel chute
    // ---------------------------------------------------------------------

    private static void BuildFunnel(string name, Vector3 topCenter, float topRadius, float bottomRadius, float verticalHeight)
    {
        var funnel = new GameObject(name);
        funnel.transform.SetParent(s_moduleGroup, false);
        funnel.transform.position = topCenter;
        funnel.transform.rotation = Quaternion.identity;

        int segments = 8;
        float panelHeight = Mathf.Sqrt(
            Mathf.Pow(verticalHeight, 2f) +
            Mathf.Pow(topRadius - bottomRadius, 2f));
        float tilt = Mathf.Atan2(topRadius - bottomRadius, verticalHeight) * Mathf.Rad2Deg;
        float midRadius = (topRadius + bottomRadius) * 0.5f;
        float panelWidth = 2f * Mathf.PI * midRadius / segments * 1.28f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * (360f / segments);
            Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
            Vector3 offset = yaw * new Vector3(0f, 0f, midRadius);
            Vector3 panelPosition = new Vector3(offset.x, -verticalHeight * 0.5f, offset.z);
            GameObject panel = Box(
                $"Funnel Panel {i + 1}",
                funnel.transform,
                panelPosition,
                new Vector3(panelWidth, panelHeight, 0.035f),
                yaw * Quaternion.Euler(tilt, 0f, 0f),
                s_mat.Chute,
                true);
            panel.GetComponent<Collider>().sharedMaterial = s_lowFriction;
        }

        // Soft brass rim pieces sit on top of the funnel and catch the key light.
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 22.5f;
            Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
            Vector3 rimOffset = yaw * new Vector3(0f, 0f, topRadius);
            Box(
                "Funnel Rim",
                funnel.transform,
                new Vector3(rimOffset.x, 0.015f, rimOffset.z),
                new Vector3(0.26f, 0.025f, 0.045f),
                yaw,
                s_mat.Ember,
                false);
        }

        // A slim, collision-free ring reads as the end of the chute without
        // creating a ledge where the marble could catch.
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 22.5f;
            Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
            Vector3 ringOffset = yaw * new Vector3(0f, 0f, bottomRadius);
            Box(
                "Funnel Exit Ring",
                funnel.transform,
                new Vector3(ringOffset.x, -verticalHeight, ringOffset.z),
                new Vector3(0.16f, 0.025f, 0.035f),
                yaw,
                s_mat.Brass,
                false);
        }

        // Emissive vertical accents turn the drop towers into visual exclamation marks.
        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + i * 90f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, topRadius + 0.02f);
            Box(
                "Funnel Glow Stud",
                funnel.transform,
                new Vector3(offset.x, -verticalHeight - 0.03f, offset.z),
                new Vector3(0.075f, 0.16f, 0.075f),
                Quaternion.identity,
                s_mat.Teal,
                false);
        }
    }

    // ---------------------------------------------------------------------
    // Cameras, build settings and cleanup
    // ---------------------------------------------------------------------

    private static void BuildCameras(Transform marble)
    {
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        Camera camera = cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.012f, 0.019f, 0.027f);
        camera.fieldOfView = 38f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 120f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        cameraGO.AddComponent<AudioListener>();

        UniversalAdditionalCameraData data = cameraGO.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
        {
            data = cameraGO.AddComponent<UniversalAdditionalCameraData>();
        }
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;

        cameraGO.transform.position = new Vector3(0f, 5.6f, -16.5f);
        cameraGO.transform.LookAt(new Vector3(0f, 4.9f, 0f));

        CameraFollowTarget follow = cameraGO.AddComponent<CameraFollowTarget>();
        follow.target = marble;
        follow.basePosition = new Vector3(0f, 5.6f, -16.5f);
        follow.horizontalFollow = 0.16f;
        follow.verticalFollow = 0.03f;
        follow.smoothing = 1.8f;

        CameraLookAt lookAt = cameraGO.AddComponent<CameraLookAt>();
        lookAt.target = marble;

        var overviewGO = new GameObject("Overview Camera (for photos)");
        Camera overview = overviewGO.AddComponent<Camera>();
        overview.clearFlags = CameraClearFlags.SolidColor;
        overview.backgroundColor = new Color(0.012f, 0.019f, 0.027f);
        overview.fieldOfView = 36f;
        overview.nearClipPlane = 0.08f;
        overview.farClipPlane = 120f;
        overview.allowHDR = true;
        overview.allowMSAA = true;
        overview.depth = -1f;
        overviewGO.transform.position = new Vector3(0f, 5.25f, -17.5f);
        overviewGO.transform.LookAt(new Vector3(0f, 4.85f, 0.05f));

        UniversalAdditionalCameraData overviewData = overviewGO.GetComponent<UniversalAdditionalCameraData>();
        if (overviewData == null)
        {
            overviewData = overviewGO.AddComponent<UniversalAdditionalCameraData>();
        }
        overviewData.renderPostProcessing = true;
        overviewData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        overviewData.antialiasingQuality = AntialiasingQuality.High;

        overviewGO.SetActive(false);
    }

    private static void AddSceneToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(scene => scene.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
