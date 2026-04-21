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
    public float pixelSize = 0.05f;
    public float depth = 0.05f;

    [Header("Resolution")]
    public int downscaleFactor = 1; // 1=full res, 2=half res, 4=quarter res, etc.

    [Header("Save Settings")]
    public string categoryFolder = "Weapons";
    public string fileName = "OptimizedVoxelItem";

    // Lists to hold mesh data
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Color> colors = new List<Color>();

    public void GeneratePreview()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Please assign an image!");
            return;
        }

        // Scale texture if downscaleFactor > 1
        Texture2D workingTexture = ScaleTexture(sourceTexture, downscaleFactor);

        // Clear the lists
        vertices.Clear();
        triangles.Clear();
        colors.Clear();

        int width = workingTexture.width;
        int height = workingTexture.height;
        Vector3 offset = new Vector3(width / 2f, height / 2f, 0) * pixelSize;

        float h = pixelSize / 2f; // Half pixel size
        float d = depth / 2f;     // Half depth

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (IsSolid(x, y, workingTexture))
                {
                    Color pColor = workingTexture.GetPixel(x, y);
                    Vector3 p = new Vector3(x * pixelSize, y * pixelSize, 0) - offset;

                    // FRONT FACE (Faces -Z direction)
                    AddFace(
                        p + new Vector3(-h, -h, -d), p + new Vector3(-h, h, -d),
                        p + new Vector3(h, h, -d), p + new Vector3(h, -h, -d), pColor);

                    // BACK FACE (Faces +Z direction)
                    AddFace(
                        p + new Vector3(h, -h, d), p + new Vector3(h, h, d),
                        p + new Vector3(-h, h, d), p + new Vector3(-h, -h, d), pColor);

                    // TOP FACE (Faces +Y direction) - Only drawn if pixel above is empty
                    if (!IsSolid(x, y + 1, workingTexture))
                        AddFace(
                            p + new Vector3(-h, h, -d), p + new Vector3(-h, h, d),
                            p + new Vector3(h, h, d), p + new Vector3(h, h, -d), pColor);

                    // BOTTOM FACE (Faces -Y direction) - Only drawn if pixel below is empty
                    if (!IsSolid(x, y - 1, workingTexture))
                        AddFace(
                            p + new Vector3(h, -h, -d), p + new Vector3(h, -h, d),
                            p + new Vector3(-h, -h, d), p + new Vector3(-h, -h, -d), pColor);

                    // LEFT FACE (Faces -X direction) - Only drawn if pixel to the left is empty
                    if (!IsSolid(x - 1, y, workingTexture))
                        AddFace(
                            p + new Vector3(-h, -h, d), p + new Vector3(-h, h, d),
                            p + new Vector3(-h, h, -d), p + new Vector3(-h, -h, -d), pColor);

                    // RIGHT FACE (Faces +X direction) - Only drawn if pixel to the right is empty
                    if (!IsSolid(x + 1, y, workingTexture))
                        AddFace(
                            p + new Vector3(h, -h, -d), p + new Vector3(h, h, -d),
                            p + new Vector3(h, h, d), p + new Vector3(h, -h, d), pColor);
                }
            }
        }

        // Create a brand new, clean Mesh with the calculated data
        Mesh optimizedMesh = new Mesh();
        optimizedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        optimizedMesh.vertices = vertices.ToArray();
        optimizedMesh.triangles = triangles.ToArray();
        optimizedMesh.colors = colors.ToArray();

        // Recalculate normals automatically so lighting works correctly
        optimizedMesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = optimizedMesh;
        if (voxelMaterial != null) GetComponent<MeshRenderer>().sharedMaterial = voxelMaterial;
    }

    // Helper function to check if there is a solid pixel at a coordinate
    private bool IsSolid(int x, int y, Texture2D texture)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height) return false;
        return texture.GetPixel(x, y).a > 0.1f;
    }

    // Helper function to add 4 vertices, 6 triangle points, and colors to the lists
    private void AddFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color col)
    {
        int vIndex = vertices.Count;

        vertices.Add(v0); vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
        colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);

        triangles.Add(vIndex); triangles.Add(vIndex + 1); triangles.Add(vIndex + 2);
        triangles.Add(vIndex); triangles.Add(vIndex + 2); triangles.Add(vIndex + 3);
    }

    // Helper function to scale texture down by a factor
    private Texture2D ScaleTexture(Texture2D source, int scaleFactor)
    {
        if (scaleFactor <= 1) return source;

        int newWidth = source.width / scaleFactor;
        int newHeight = source.height / scaleFactor;
        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                Color c = source.GetPixel(x * scaleFactor, y * scaleFactor);
                scaled.SetPixel(x, y, c);
            }
        }
        scaled.Apply();
        return scaled;
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

        string meshFolderAbs = Application.dataPath + "/Models/Meshes/" + categoryFolder;
        string prefabFolderAbs = Application.dataPath + "/Models/Prefabs/" + categoryFolder;

        if (!Directory.Exists(meshFolderAbs)) Directory.CreateDirectory(meshFolderAbs);
        if (!Directory.Exists(prefabFolderAbs)) Directory.CreateDirectory(prefabFolderAbs);
        AssetDatabase.Refresh();

        string uniqueFileName = fileName;
        int counter = 1;
        string meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
        string prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";

        while (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null || AssetDatabase.LoadAssetAtPath<Object>(meshPath) != null)
        {
            uniqueFileName = fileName + "_" + counter;
            meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
            prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";
            counter++;
        }

        Mesh meshToSave = Instantiate(mf.sharedMesh);
        AssetDatabase.CreateAsset(meshToSave, meshPath);
        AssetDatabase.SaveAssets();

        GameObject prefabSkeleton = new GameObject(uniqueFileName);
        MeshFilter newMF = prefabSkeleton.AddComponent<MeshFilter>();
        MeshRenderer newMR = prefabSkeleton.AddComponent<MeshRenderer>();
        newMF.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        newMR.sharedMaterial = voxelMaterial;

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
