using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SimpleSpriteExtruder : MonoBehaviour
{
    [Header("Voxel Settings")]
    public Texture2D sourceTexture;
    public Material voxelMaterial;
    public float pixelSize = 0.1f;
    public float depth = 0.1f;

    [Header("Resolution")]
    public int downscaleFactor = 1;

    [Header("Optimization")]
    public bool useGreedyMeshing = true;
    public float colorMatchTolerance = 4f;

    [Header("Generated Depth Maps")]
    public bool generateDepthMaps = true;
    [Range(0f, 4f)] public float generatedNormalStrength = 1.5f;
    [Range(0f, 1f)] public float generatedHeightAffect = 0.25f;
    [Range(0.1f, 4f)] public float generatedHeightContrast = 1.2f;
    [Range(-1f, 1f)] public float generatedHeightBias = 0f;
    [Range(0, 3)] public int generatedHeightBlurRadius = 1;
    public bool invertGeneratedNormalY = false;
    public bool useGeneratedParallax = true;
    [Range(0f, 0.1f)] public float generatedParallaxStrength = 0.02f;

    [Header("Save Settings")]
    public string categoryFolder = "testFolder";
    public string fileName = "item";

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Color> colors = new List<Color>();
    private List<Vector2> uvs = new List<Vector2>();
    private Texture2D lastGeneratedTexture;
    private Texture2D lastGeneratedNormalMap;
    private Texture2D lastGeneratedHeightMap;
    private Material generatedPreviewMaterial;

    public void GeneratePreview()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Please assign an image!");
            return;
        }

        sourceTexture.filterMode = FilterMode.Point;
        sourceTexture.wrapMode = TextureWrapMode.Clamp;
        sourceTexture.anisoLevel = 0;

        Texture2D workingTexture = ScaleTexture(sourceTexture, downscaleFactor);
        lastGeneratedTexture = workingTexture;

        if (generateDepthMaps)
        {
            lastGeneratedHeightMap = GenerateHeightMap(workingTexture, generatedHeightContrast, generatedHeightBias, generatedHeightBlurRadius);
            lastGeneratedNormalMap = GenerateNormalMapFromHeight(lastGeneratedHeightMap, generatedNormalStrength, invertGeneratedNormalY);
        }
        else
        {
            lastGeneratedHeightMap = null;
            lastGeneratedNormalMap = null;
        }

        if (useGreedyMeshing)
            GenerateGreedyMesh(workingTexture);
        else
            GenerateStandardMesh(workingTexture);
    }

    private void GenerateStandardMesh(Texture2D workingTexture)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        uvs.Clear();

        int width = workingTexture.width;
        int height = workingTexture.height;
        Vector3 offset = new Vector3(width / 2f, height / 2f, 0) * pixelSize;

        float h = pixelSize / 2f;
        float d = depth / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsSolid(x, y, workingTexture))
                {
                    Color pColor = workingTexture.GetPixel(x, y);
                    Vector3 p = new Vector3(x * pixelSize, y * pixelSize, 0) - offset;

                    if (!IsSolid(x, y - 1, workingTexture))
                        AddFace(p + new Vector3(h, -h, -d), p + new Vector3(h, -h, d), p + new Vector3(-h, -h, d), p + new Vector3(-h, -h, -d), pColor);

                    if (!IsSolid(x, y + 1, workingTexture))
                        AddFace(p + new Vector3(-h, h, -d), p + new Vector3(-h, h, d), p + new Vector3(h, h, d), p + new Vector3(h, h, -d), pColor);

                    if (!IsSolid(x - 1, y, workingTexture))
                        AddFace(p + new Vector3(-h, -h, d), p + new Vector3(-h, h, d), p + new Vector3(-h, h, -d), p + new Vector3(-h, -h, -d), pColor);

                    if (!IsSolid(x + 1, y, workingTexture))
                        AddFace(p + new Vector3(h, -h, -d), p + new Vector3(h, h, -d), p + new Vector3(h, h, d), p + new Vector3(h, -h, d), pColor);

                    AddFace(p + new Vector3(-h, -h, -d), p + new Vector3(-h, h, -d), p + new Vector3(h, h, -d), p + new Vector3(h, -h, -d), pColor);
                    AddFace(p + new Vector3(h, -h, d), p + new Vector3(h, h, d), p + new Vector3(-h, h, d), p + new Vector3(-h, -h, d), pColor);
                }
            }
        }

        CreateMesh();
    }

    private void GenerateGreedyMesh(Texture2D workingTexture)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();
        uvs.Clear();

        int width = workingTexture.width;
        int height = workingTexture.height;
        Vector3 offset = new Vector3(width / 2f, height / 2f, 0) * pixelSize;
        float d = depth / 2f;

        bool[,] processed0 = new bool[width, height];
        bool[,] processed1 = new bool[width, height];
        bool[,] processed2 = new bool[width, height];
        bool[,] processed3 = new bool[width, height];
        bool[,] processed4 = new bool[width, height];
        bool[,] processed5 = new bool[width, height];

        for (int direction = 0; direction < 6; direction++)
        {
            bool[,] processed = direction switch
            {
                0 => processed0,
                1 => processed1,
                2 => processed2,
                3 => processed3,
                4 => processed4,
                5 => processed5,
                _ => processed0
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (processed[x, y] || !IsSolid(x, y, workingTexture))
                        continue;

                    if (!ShouldGenerateFace(x, y, direction, workingTexture))
                        continue;

                    Color faceColor = workingTexture.GetPixel(x, y);

                    int xSpan = 1;
                    while (x + xSpan < width && !processed[x + xSpan, y] && IsSolid(x + xSpan, y, workingTexture) && ShouldGenerateFace(x + xSpan, y, direction, workingTexture) && ColorsMatch(workingTexture.GetPixel(x + xSpan, y), faceColor))
                        xSpan++;

                    int ySpan = 1;
                    while (y + ySpan < height)
                    {
                        bool canExpand = true;
                        for (int xx = x; xx < x + xSpan; xx++)
                        {
                            if (processed[xx, y + ySpan] || !IsSolid(xx, y + ySpan, workingTexture) || !ShouldGenerateFace(xx, y + ySpan, direction, workingTexture) || !ColorsMatch(workingTexture.GetPixel(xx, y + ySpan), faceColor))
                            {
                                canExpand = false;
                                break;
                            }
                        }
                        if (canExpand) ySpan++;
                        else break;
                    }

                    Vector3 p0 = new Vector3(x * pixelSize, y * pixelSize, 0) - offset;
                    Vector3 p1 = new Vector3((x + xSpan) * pixelSize, (y + ySpan) * pixelSize, 0) - offset;

                    GenerateFaceForDirection(direction, p0, p1, d, faceColor, x, y, xSpan, ySpan, width, height);

                    for (int xx = x; xx < x + xSpan; xx++)
                        for (int yy = y; yy < y + ySpan; yy++)
                            processed[xx, yy] = true;
                }
            }
        }

        CreateMesh();
    }

    private bool ShouldGenerateFace(int x, int y, int direction, Texture2D texture)
    {
        return direction switch
        {
            0 => !IsSolid(x, y - 1, texture),
            1 => !IsSolid(x, y + 1, texture),
            2 => !IsSolid(x - 1, y, texture),
            3 => !IsSolid(x + 1, y, texture),
            4 => true,
            5 => true,
            _ => false
        };
    }

    private void GenerateFaceForDirection(int direction, Vector3 p0, Vector3 p1, float d, Color col, int startX, int startY, int xSpan, int ySpan, int texWidth, int texHeight)
    {
        float uvX0 = (float)startX / texWidth;
        float uvX1 = (float)(startX + xSpan) / texWidth;
        float uvY0 = (float)startY / texHeight;
        float uvY1 = (float)(startY + ySpan) / texHeight;

        switch (direction)
        {
            case 0:
                AddFace(new Vector3(p1.x, p0.y, -d), new Vector3(p1.x, p0.y, d), new Vector3(p0.x, p0.y, d), new Vector3(p0.x, p0.y, -d), col,
                    new Vector2(uvX1, uvY0), new Vector2(uvX1, uvY0), new Vector2(uvX0, uvY0), new Vector2(uvX0, uvY0));
                break;
            case 1:
                AddFace(new Vector3(p0.x, p1.y, -d), new Vector3(p0.x, p1.y, d), new Vector3(p1.x, p1.y, d), new Vector3(p1.x, p1.y, -d), col,
                    new Vector2(uvX0, uvY1), new Vector2(uvX0, uvY1), new Vector2(uvX1, uvY1), new Vector2(uvX1, uvY1));
                break;
            case 2:
                AddFace(new Vector3(p0.x, p0.y, d), new Vector3(p0.x, p1.y, d), new Vector3(p0.x, p1.y, -d), new Vector3(p0.x, p0.y, -d), col,
                    new Vector2(uvX0, uvY0), new Vector2(uvX0, uvY1), new Vector2(uvX0, uvY1), new Vector2(uvX0, uvY0));
                break;
            case 3:
                AddFace(new Vector3(p1.x, p0.y, -d), new Vector3(p1.x, p1.y, -d), new Vector3(p1.x, p1.y, d), new Vector3(p1.x, p0.y, d), col,
                    new Vector2(uvX1, uvY0), new Vector2(uvX1, uvY1), new Vector2(uvX1, uvY1), new Vector2(uvX1, uvY0));
                break;
            case 4:
                AddFace(new Vector3(p0.x, p0.y, -d), new Vector3(p0.x, p1.y, -d), new Vector3(p1.x, p1.y, -d), new Vector3(p1.x, p0.y, -d), col,
                    new Vector2(uvX0, uvY0), new Vector2(uvX0, uvY1), new Vector2(uvX1, uvY1), new Vector2(uvX1, uvY0));
                break;
            case 5:
                AddFace(new Vector3(p1.x, p0.y, d), new Vector3(p1.x, p1.y, d), new Vector3(p0.x, p1.y, d), new Vector3(p0.x, p0.y, d), col,
                    new Vector2(uvX1, uvY0), new Vector2(uvX1, uvY1), new Vector2(uvX0, uvY1), new Vector2(uvX0, uvY0));
                break;
        }
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Vector4.Distance(a, b) < colorMatchTolerance;
    }

    private void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        ApplyVoxelMaterial(GetComponent<MeshRenderer>(), true);
    }

    private void ConfigureVoxelMaterial(Material mat, Texture2D textureForMaterial, Texture2D normalMapForMaterial, Texture2D heightMapForMaterial)
    {
        if (mat == null) return;

        if (textureForMaterial != null)
        {
            textureForMaterial.filterMode = FilterMode.Point;
            textureForMaterial.wrapMode = TextureWrapMode.Clamp;
            textureForMaterial.anisoLevel = 0;
        }

        if (textureForMaterial != null && mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", textureForMaterial);
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
        }

        if (normalMapForMaterial != null)
        {
            normalMapForMaterial.filterMode = FilterMode.Point;
            normalMapForMaterial.wrapMode = TextureWrapMode.Clamp;
            normalMapForMaterial.anisoLevel = 0;
        }

        if (heightMapForMaterial != null)
        {
            heightMapForMaterial.filterMode = FilterMode.Point;
            heightMapForMaterial.wrapMode = TextureWrapMode.Clamp;
            heightMapForMaterial.anisoLevel = 0;
        }

        if (mat.HasProperty("_NormalMap"))
            mat.SetTexture("_NormalMap", normalMapForMaterial);

        if (mat.HasProperty("_HeightMap"))
            mat.SetTexture("_HeightMap", heightMapForMaterial);

        if (mat.HasProperty("_UseTexture"))
            mat.SetFloat("_UseTexture", textureForMaterial != null ? 1f : 0f);

        if (mat.HasProperty("_UseVertexColor"))
            mat.SetFloat("_UseVertexColor", 0f);

        if (mat.HasProperty("_UseNormalMap"))
            mat.SetFloat("_UseNormalMap", normalMapForMaterial != null ? 1f : 0f);

        if (mat.HasProperty("_UseHeightMap"))
            mat.SetFloat("_UseHeightMap", heightMapForMaterial != null ? 1f : 0f);

        if (mat.HasProperty("_NormalStrength"))
            mat.SetFloat("_NormalStrength", generatedNormalStrength);

        if (mat.HasProperty("_HeightAffect"))
            mat.SetFloat("_HeightAffect", generatedHeightAffect);

        if (mat.HasProperty("_UseParallax"))
            mat.SetFloat("_UseParallax", (heightMapForMaterial != null && useGeneratedParallax) ? 1f : 0f);

        if (mat.HasProperty("_ParallaxStrength"))
            mat.SetFloat("_ParallaxStrength", generatedParallaxStrength);
    }

    private void ApplyVoxelMaterial(MeshRenderer meshRenderer, bool createUniqueInstance)
    {
        if (meshRenderer == null || voxelMaterial == null) return;

        Material targetMaterial;

        if (createUniqueInstance)
        {
            if (generatedPreviewMaterial == null)
            {
                generatedPreviewMaterial = new Material(voxelMaterial);
                generatedPreviewMaterial.name = voxelMaterial.name + "_" + gameObject.name + "_Instance";
            }

            targetMaterial = generatedPreviewMaterial;
        }
        else
        {
            targetMaterial = voxelMaterial;
        }

        Texture2D textureForMaterial = lastGeneratedTexture != null ? lastGeneratedTexture : sourceTexture;
        ConfigureVoxelMaterial(targetMaterial, textureForMaterial, lastGeneratedNormalMap, lastGeneratedHeightMap);
        meshRenderer.sharedMaterial = targetMaterial;
    }

    private bool IsSolid(int x, int y, Texture2D texture)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) return false;
        return texture.GetPixel(x, y).a > 0.1f;
    }

    private void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color col, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
    {
        int vIndex = vertices.Count;

        vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
        colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
        uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);

        triangles.Add(vIndex); triangles.Add(vIndex + 1); triangles.Add(vIndex + 2);
        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);
    }

    private void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color col)
    {
        AddFace(v0, v1, v2, v3, col, Vector2.zero, Vector2.one, Vector2.one, Vector2.zero);
    }

    private Texture2D ScaleTexture(Texture2D source, int scaleFactor)
    {
        int step = Mathf.Max(1, scaleFactor);

        int newWidth = Mathf.Max(1, source.width / step);
        int newHeight = Mathf.Max(1, source.height / step);

        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        scaled.filterMode = FilterMode.Point;
        scaled.wrapMode = TextureWrapMode.Clamp;
        scaled.anisoLevel = 0;

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                int srcX = Mathf.Min(source.width - 1, x * step);
                int srcY = Mathf.Min(source.height - 1, y * step);
                scaled.SetPixel(x, y, source.GetPixel(srcX, srcY));
            }
        }

        scaled.Apply(false, false);
        return scaled;
    }

    private Texture2D GenerateHeightMap(Texture2D source, float contrast, float bias, int blurRadius)
    {
        int w = source.width;
        int h = source.height;
        Texture2D heightMap = new Texture2D(w, h, TextureFormat.RGBA32, false);
        heightMap.filterMode = FilterMode.Point;
        heightMap.wrapMode = TextureWrapMode.Clamp;
        heightMap.anisoLevel = 0;

        float[] heights = new float[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = source.GetPixel(x, y);
                float v = c.grayscale * c.a;
                v = (v - 0.5f) * contrast + 0.5f + bias;
                v = Mathf.Clamp01(v);
                heights[y * w + x] = v;
            }
        }

        if (blurRadius > 0)
        {
            float[] temp = new float[w * h];
            for (int pass = 0; pass < blurRadius; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        int count = 0;
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            int sy = Mathf.Clamp(y + oy, 0, h - 1);
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int sx = Mathf.Clamp(x + ox, 0, w - 1);
                                sum += heights[sy * w + sx];
                                count++;
                            }
                        }

                        temp[y * w + x] = sum / count;
                    }
                }

                var swap = heights;
                heights = temp;
                temp = swap;
            }
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float hv = heights[y * w + x];
                float a = source.GetPixel(x, y).a;
                heightMap.SetPixel(x, y, new Color(hv, hv, hv, a));
            }
        }

        heightMap.Apply(false, false);
        return heightMap;
    }

    private Texture2D GenerateNormalMapFromHeight(Texture2D heightMap, float strength, bool invertY)
    {
        Texture2D normalMap = new Texture2D(heightMap.width, heightMap.height, TextureFormat.RGBA32, false);
        normalMap.filterMode = FilterMode.Point;
        normalMap.wrapMode = TextureWrapMode.Clamp;
        normalMap.anisoLevel = 0;

        int w = heightMap.width;
        int h = heightMap.height;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float hL = heightMap.GetPixel(Mathf.Max(0, x - 1), y).r;
                float hR = heightMap.GetPixel(Mathf.Min(w - 1, x + 1), y).r;
                float hD = heightMap.GetPixel(x, Mathf.Max(0, y - 1)).r;
                float hU = heightMap.GetPixel(x, Mathf.Min(h - 1, y + 1)).r;

                float dx = (hL - hR) * strength;
                float dy = (hD - hU) * strength;

                if (invertY)
                    dy = -dy;

                Vector3 n = new Vector3(dx, dy, 1f).normalized;
                Color enc = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                normalMap.SetPixel(x, y, enc);
            }
        }

        normalMap.Apply(false, false);
        return normalMap;
    }

    public void SaveAsPrefab()
    {
#if UNITY_EDITOR
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == null)
        {
            Debug.LogError("There is no model to save! Press the Preview button first.");
            return;
        }

        if (voxelMaterial == null)
        {
            Debug.LogError("Please assign a voxel material before saving.");
            return;
        }

        string meshFolderAbs = Application.dataPath + "/Models/Meshes/" + categoryFolder;
        string prefabFolderAbs = Application.dataPath + "/Models/Prefabs/" + categoryFolder;
        string materialFolderAbs = Application.dataPath + "/Models/Materials/" + categoryFolder;
        string textureFolderAbs = Application.dataPath + "/Models/Textures/" + categoryFolder;

        if (!Directory.Exists(meshFolderAbs)) Directory.CreateDirectory(meshFolderAbs);
        if (!Directory.Exists(prefabFolderAbs)) Directory.CreateDirectory(prefabFolderAbs);
        if (!Directory.Exists(materialFolderAbs)) Directory.CreateDirectory(materialFolderAbs);
        if (!Directory.Exists(textureFolderAbs)) Directory.CreateDirectory(textureFolderAbs);
        AssetDatabase.Refresh();

        string uniqueFileName = fileName;
        int counter = 1;
        string meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
        string prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";
        string materialPath = "Assets/Models/Materials/" + categoryFolder + "/" + uniqueFileName + "_Mat.mat";
        string texturePath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Tex.asset";
        string normalPath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Normal.asset";
        string heightPath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Height.asset";

        while (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(meshPath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(materialPath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(texturePath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(normalPath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(heightPath) != null)
        {
            uniqueFileName = fileName + "_" + counter;
            meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
            prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";
            materialPath = "Assets/Models/Materials/" + categoryFolder + "/" + uniqueFileName + "_Mat.mat";
            texturePath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Tex.asset";
            normalPath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Normal.asset";
            heightPath = "Assets/Models/Textures/" + categoryFolder + "/" + uniqueFileName + "_Height.asset";
            counter++;
        }

        Mesh meshToSave = Instantiate(mf.sharedMesh);
        AssetDatabase.CreateAsset(meshToSave, meshPath);

        Texture2D textureForMaterial = lastGeneratedTexture != null ? lastGeneratedTexture : sourceTexture;
        Texture2D textureAssetRef = textureForMaterial;
        Texture2D normalAssetRef = lastGeneratedNormalMap;
        Texture2D heightAssetRef = lastGeneratedHeightMap;

        if (textureForMaterial != null && textureForMaterial != sourceTexture)
        {
            Texture2D textureToSave = Instantiate(textureForMaterial);
            textureToSave.filterMode = FilterMode.Point;
            textureToSave.wrapMode = TextureWrapMode.Clamp;
            textureToSave.anisoLevel = 0;
            AssetDatabase.CreateAsset(textureToSave, texturePath);
            textureAssetRef = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (textureAssetRef != null)
            {
                textureAssetRef.filterMode = FilterMode.Point;
                textureAssetRef.wrapMode = TextureWrapMode.Clamp;
                textureAssetRef.anisoLevel = 0;
            }
        }

        if (lastGeneratedNormalMap != null)
        {
            Texture2D normalToSave = Instantiate(lastGeneratedNormalMap);
            normalToSave.filterMode = FilterMode.Point;
            normalToSave.wrapMode = TextureWrapMode.Clamp;
            normalToSave.anisoLevel = 0;
            AssetDatabase.CreateAsset(normalToSave, normalPath);
            normalAssetRef = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        }

        if (lastGeneratedHeightMap != null)
        {
            Texture2D heightToSave = Instantiate(lastGeneratedHeightMap);
            heightToSave.filterMode = FilterMode.Point;
            heightToSave.wrapMode = TextureWrapMode.Clamp;
            heightToSave.anisoLevel = 0;
            AssetDatabase.CreateAsset(heightToSave, heightPath);
            heightAssetRef = AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath);
        }

        Material materialToSave = new Material(voxelMaterial);
        ConfigureVoxelMaterial(materialToSave, textureAssetRef, normalAssetRef, heightAssetRef);
        AssetDatabase.CreateAsset(materialToSave, materialPath);
        AssetDatabase.SaveAssets();

        GameObject prefabSkeleton = new GameObject(uniqueFileName);
        MeshFilter newMF = prefabSkeleton.AddComponent<MeshFilter>();
        MeshRenderer newMR = prefabSkeleton.AddComponent<MeshRenderer>();
        newMF.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        newMR.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        PrefabUtility.SaveAsPrefabAsset(prefabSkeleton, prefabPath);
        DestroyImmediate(prefabSkeleton);
        Debug.Log("Optimized object safely saved as: " + uniqueFileName);
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleSpriteExtruder))]
public class SimpleSpriteExtruderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        SimpleSpriteExtruder script = (SimpleSpriteExtruder)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Voxel Generation Operations", EditorStyles.boldLabel);

        if (GUILayout.Button("1 - Generate Optimized Preview", GUILayout.Height(35))) script.GeneratePreview();
        EditorGUILayout.Space(5);
        if (GUILayout.Button("2 - Save Ready Model to Folders", GUILayout.Height(35))) script.SaveAsPrefab();
    }
}
#endif

