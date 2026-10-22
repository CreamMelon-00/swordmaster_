using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// This independent Editor project imports only the four final production icons.
// It does not load production scenes, runtime assemblies, packages or settings.
public static class SkillIconRenderValidation
{
    private const int Canvas = 96;
    private static readonly int[] Roles = { 36, 37, 38, 39 };
    private static readonly int[] Sizes = { 40, 44, 48, 52 };

    [Serializable] private sealed class Icon
    {
        public int role;
        public string pngSha256;
        public string importedMetaSha256;
        public int textureWidth, textureHeight;
        public int rectWidth, rectHeight;
        public float pivotX, pivotY, pixelsPerUnit;
        public int meshVertexCount;
        public string filter, wrap, meshType, compression;
        public bool mipmaps, binaryAlpha, exactFourTimesClusters;
        public int opaqueColorCount;
        public int[] sourceAlphaBounds;
    }

    [Serializable] private sealed class Shot
    {
        public int role, displayedPixels;
        public string file;
        public int[] renderedAlphaBounds;
        public int opaquePixels, fractionalAlphaPixels, foreignPalettePixels;
    }

    [Serializable] private sealed class Report
    {
        public string unity, graphics, device, pipeline, colorSpace;
        public string scope = "Isolated import and SpriteRenderer rendering of four new skill resources. No production game, UI canvas, URP, Play Mode or player build validation.";
        public List<Icon> icons = new List<Icon>();
        public List<Shot> shots = new List<Shot>();
    }

    public static void Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
        Directory.CreateDirectory(output);
        GameObject cameraObject = null, spriteObject = null;
        Material material = null;
        var report = new Report();
        try
        {
            Require(Application.unityVersion == "6000.5.9f1", "Unexpected Unity version " + Application.unityVersion);
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11,
                "Expected native Direct3D11, received " + SystemInfo.graphicsDeviceType);
            Require(GraphicsSettings.currentRenderPipeline == null, "Isolated project must use Built-in pipeline.");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.antiAliasing = 0;
            report.unity = Application.unityVersion;
            report.graphics = SystemInfo.graphicsDeviceType.ToString();
            report.device = SystemInfo.graphicsDeviceName;
            report.pipeline = "Built-in / Sprites/Default";
            report.colorSpace = QualitySettings.activeColorSpace.ToString();

            cameraObject = new GameObject("Isolated skill icon camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = Canvas * .5f; // One world unit is one rendered pixel.
            camera.aspect = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0, 0, -10);

            Shader shader = Shader.Find("Sprites/Default");
            Require(shader != null && shader.isSupported, "Sprites/Default is missing or unsupported.");
            material = new Material(shader);
            spriteObject = new GameObject("Actual imported resource sprite");
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.color = Color.white;

            foreach (int role in Roles)
            {
                string assetPath = "Assets/Resources/SkillRoles/role" + role + ".png";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Require(importer != null, "Missing importer " + assetPath);
                Sprite sprite = Resources.Load<Sprite>("SkillRoles/role" + role);
                Require(sprite != null, "Resources.Load<Sprite> failed for role" + role);
                ValidateImporter(importer, sprite, role);

                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Require(ImageConversion.LoadImage(source, File.ReadAllBytes(fullPath), false), "PNG decoding failed.");
                    Color32[] pixels = source.GetPixels32();
                    HashSet<uint> palette = SourcePaletteAndValidate(pixels, source.width, source.height, role);
                    int[] bounds = AlphaBounds(pixels, source.width, source.height);
                    Require(bounds[0] >= 8 && bounds[1] >= 8 && bounds[2] <= 184 && bounds[3] <= 184,
                        "role" + role + " lost the unified transparent padding.");
                    Require(bounds[2] - bounds[0] <= 176 && bounds[3] - bounds[1] <= 176,
                        "role" + role + " exceeds 44 logical pixels.");
                    report.icons.Add(new Icon {
                        role = role, pngSha256 = Hash(fullPath), importedMetaSha256 = Hash(fullPath + ".meta"),
                        textureWidth = sprite.texture.width, textureHeight = sprite.texture.height,
                        rectWidth = (int)sprite.rect.width, rectHeight = (int)sprite.rect.height,
                        pivotX = sprite.pivot.x, pivotY = sprite.pivot.y, pixelsPerUnit = sprite.pixelsPerUnit,
                        meshVertexCount = sprite.vertices.Length,
                        filter = sprite.texture.filterMode.ToString(), wrap = sprite.texture.wrapMode.ToString(),
                        meshType = MeshType(importer).ToString(), compression = importer.textureCompression.ToString(),
                        mipmaps = importer.mipmapEnabled, binaryAlpha = true, exactFourTimesClusters = true,
                        opaqueColorCount = palette.Count, sourceAlphaBounds = bounds
                    });

                    renderer.sprite = sprite;
                    foreach (int size in Sizes)
                    {
                        spriteObject.transform.localScale = Vector3.one * (size / sprite.bounds.size.x);
                        report.shots.Add(Capture(camera, role, size, palette, output));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }

            Require(report.icons.Count == 4 && report.shots.Count == 16, "Incomplete validation set.");
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("SKILL ICON RENDER VALIDATION PASS: 4 native Resource sprites imported; 16 D3D11 captures at 40/44/48/52px. " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
            File.WriteAllText(Path.Combine(output, "partial-report.json"), JsonUtility.ToJson(report, true));
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (spriteObject != null) UnityEngine.Object.DestroyImmediate(spriteObject);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    private static void ValidateImporter(TextureImporter importer, Sprite sprite, int role)
    {
        string label = "role" + role + ": ";
        Require(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single, label + "must import as a single Sprite.");
        Require(MeshType(importer) == SpriteMeshType.FullRect && sprite.vertices.Length == 4, label + "must retain its complete rectangular canvas.");
        Require(sprite.rect.size == new Vector2(192, 192) && sprite.texture.width == 192 && sprite.texture.height == 192, label + "canvas is not 192 square.");
        Require(sprite.pivot == new Vector2(96, 96) && importer.spritePivot == new Vector2(.5f, .5f), label + "pivot is not centered.");
        Require(Mathf.Approximately(sprite.pixelsPerUnit, 100), label + "unexpected PPU.");
        Require(!importer.mipmapEnabled && sprite.texture.mipmapCount == 1, label + "mipmaps must be disabled.");
        Require(importer.filterMode == FilterMode.Point && sprite.texture.filterMode == FilterMode.Point, label + "filter must be Point.");
        Require(importer.wrapMode == TextureWrapMode.Clamp && sprite.texture.wrapMode == TextureWrapMode.Clamp, label + "wrap must be Clamp.");
        Require(importer.textureCompression == TextureImporterCompression.Uncompressed, label + "compression must be off.");
        Require(!importer.GetPlatformTextureSettings("Standalone").overridden ||
            importer.GetPlatformTextureSettings("Standalone").textureCompression == TextureImporterCompression.Uncompressed,
            label + "Standalone override enables compression.");
    }

    private static HashSet<uint> SourcePaletteAndValidate(Color32[] pixels, int width, int height, int role)
    {
        Require(width == 192 && height == 192, "role" + role + " source PNG is not 192 square.");
        var palette = new HashSet<uint>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 p = pixels[y * width + x];
            Require(p.a == 0 || p.a == 255, "role" + role + " source contains fractional alpha.");
            Color32 block = pixels[(y / 4 * 4) * width + x / 4 * 4];
            Require(p.Equals(block), "role" + role + " is not an exact nearest-neighbor 4x bitmap.");
            if (p.a > 0) palette.Add(Pack(p));
        }
        Require(palette.Count > 0 && palette.Count <= 48, "role" + role + " palette exceeds specification.");
        return palette;
    }

    private static Shot Capture(Camera camera, int role, int size, HashSet<uint> palette, string output)
    {
        var target = new RenderTexture(Canvas, Canvas, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1, filterMode = FilterMode.Point };
        var readback = new Texture2D(Canvas, Canvas, TextureFormat.RGBA32, false, false);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Canvas, Canvas), 0, 0);
            readback.Apply(false, false);
            string file = "role" + role + "-" + size + "px.png";
            File.WriteAllBytes(Path.Combine(output, file), readback.EncodeToPNG());
            Color32[] pixels = readback.GetPixels32();
            int opaque = 0, fractional = 0, foreign = 0;
            int left = (Canvas - size) / 2, right = left + size;
            for (int y = 0; y < Canvas; y++)
            for (int x = 0; x < Canvas; x++)
            {
                Color32 p = pixels[y * Canvas + x];
                if (p.a == 0) continue;
                if (p.a != 255) fractional++;
                opaque++;
                Require(x >= left && x < right && y >= left && y < right,
                    "Rendered pixels escape the full sprite canvas.");
                if (!MatchesPalette(p, palette)) foreign++;
            }
            Require(opaque > size * size / 4, "Native capture is empty or incomplete.");
            Require(fractional == 0, "Native Point render contains antialiased alpha.");
            Require(foreign == 0, "Native Point render contains interpolated or tinted colors.");
            int[] bounds = AlphaBounds(pixels, Canvas, Canvas);
            Require(bounds[0] > left && bounds[1] > left && bounds[2] < right && bounds[3] < right,
                "Native capture lost transparent margins around its full canvas.");
            return new Shot { role = role, displayedPixels = size, file = file,
                renderedAlphaBounds = bounds, opaquePixels = opaque,
                fractionalAlphaPixels = fractional, foreignPalettePixels = foreign };
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(readback);
        }
    }

    private static bool MatchesPalette(Color32 pixel, HashSet<uint> palette)
    {
        // Linear-to-sRGB GPU round-trips may differ from the PNG by one or two
        // code values. Point sampling must still select a native source color.
        foreach (uint p in palette)
            if (Math.Abs(pixel.r - (int)(p >> 16 & 255)) <= 2 &&
                Math.Abs(pixel.g - (int)(p >> 8 & 255)) <= 2 &&
                Math.Abs(pixel.b - (int)(p & 255)) <= 2) return true;
        return false;
    }

    private static int[] AlphaBounds(Color32[] pixels, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (pixels[y * width + x].a != 0)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
        Require(maxX >= 0, "Empty sprite alpha.");
        return new[] { minX, minY, maxX + 1, maxY + 1 };
    }

    private static SpriteMeshType MeshType(TextureImporter importer)
    {
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        return settings.spriteMeshType;
    }

    private static uint Pack(Color32 p) => (uint)(p.r << 16 | p.g << 8 | p.b);
    private static string Hash(string path)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
