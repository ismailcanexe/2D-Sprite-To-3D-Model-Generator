using UnityEngine;
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
    public float depth = 0.2f;

    [Header("Save Settings")]
    [Tooltip("Which category to save into? (e.g., Weapons, Buildings)")]
    public string categoryFolder = "Default";

    [Tooltip("The name of the file to be saved")]
    public string fileName = "Default";

    public void GeneratePreview()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("Please assign an image!");
            return;
        }

        int width = sourceTexture.width;
        int height = sourceTexture.height;
        CombineInstance[] combine = new CombineInstance[width * height];
        int voxelCount = 0;
        Vector3 offset = new Vector3(width / 2f, height / 2f, 0) * pixelSize;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color pixelColor = sourceTexture.GetPixel(x, y);

                if (pixelColor.a > 0.1f)
                {
                    GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tempCube.transform.localScale = new Vector3(pixelSize, pixelSize, depth);
                    tempCube.transform.position = new Vector3(x * pixelSize, y * pixelSize, 0) - offset;

                    MeshFilter filter = tempCube.GetComponent<MeshFilter>();
                    Mesh tempMesh = filter.sharedMesh;

                    Color[] vertexColors = new Color[tempMesh.vertices.Length];
                    for (int i = 0; i < vertexColors.Length; i++)
                    {
                        vertexColors[i] = pixelColor;
                    }

                    Mesh coloredMesh = Instantiate(tempMesh);
                    coloredMesh.colors = vertexColors;

                    combine[voxelCount].mesh = coloredMesh;
                    combine[voxelCount].transform = tempCube.transform.localToWorldMatrix;
                    voxelCount++;

                    DestroyImmediate(tempCube);
                }
            }
        }

        CombineInstance[] finalCombine = new CombineInstance[voxelCount];
        System.Array.Copy(combine, finalCombine, voxelCount);

        Mesh newMesh = new Mesh();
        newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        newMesh.CombineMeshes(finalCombine);

        GetComponent<MeshFilter>().mesh = newMesh;

        if (voxelMaterial != null)
        {
            GetComponent<MeshRenderer>().sharedMaterial = voxelMaterial;
        }
        else
        {
            Debug.LogWarning("Material is not assigned!");
        }
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

        // Folder creation operations
        string meshFolderAbs = Application.dataPath + "/Models/Meshes/" + categoryFolder;
        string prefabFolderAbs = Application.dataPath + "/Models/Prefabs/" + categoryFolder;

        if (!Directory.Exists(meshFolderAbs)) Directory.CreateDirectory(meshFolderAbs);
        if (!Directory.Exists(prefabFolderAbs)) Directory.CreateDirectory(prefabFolderAbs);

        AssetDatabase.Refresh();

        // --- UNIQUE FILE NAMING SYSTEM ---
        string uniqueFileName = fileName;
        int counter = 1;

        string meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
        string prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";

        // If a prefab OR mesh with this name already exists, add a number to the end and try again
        while (AssetDatabase.LoadAssetAtPath<Object>(prefabPath) != null ||
               AssetDatabase.LoadAssetAtPath<Object>(meshPath) != null)
        {
            uniqueFileName = fileName + "_" + counter;
            meshPath = "Assets/Models/Meshes/" + categoryFolder + "/" + uniqueFileName + "_Mesh.asset";
            prefabPath = "Assets/Models/Prefabs/" + categoryFolder + "/" + uniqueFileName + ".prefab";
            counter++;
        }

        // 1. Save Mesh (Now saves with a unique name without overwriting)
        Mesh meshToSave = Instantiate(mf.sharedMesh);
        AssetDatabase.CreateAsset(meshToSave, meshPath);
        AssetDatabase.SaveAssets();

        // 2. Save Prefab
        GameObject prefabSkeleton = new GameObject(uniqueFileName);
        MeshFilter newMF = prefabSkeleton.AddComponent<MeshFilter>();
        MeshRenderer newMR = prefabSkeleton.AddComponent<MeshRenderer>();

        newMF.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        newMR.sharedMaterial = voxelMaterial;

        PrefabUtility.SaveAsPrefabAsset(prefabSkeleton, prefabPath);
        DestroyImmediate(prefabSkeleton);

        Debug.Log("Done! The new object was safely saved as '" + uniqueFileName + "'.");
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

        if (GUILayout.Button("1 - Generate / Update Preview", GUILayout.Height(35)))
        {
            script.GeneratePreview();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("2 - Save Ready Model to Folders", GUILayout.Height(35)))
        {
            script.SaveAsPrefab();
        }
    }
}
#endif