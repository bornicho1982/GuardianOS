using UnityEngine;
using GLTFast;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Loads GLTF/FBX models and applies Destiny Dye materials
/// </summary>
public class CharacterLoader : MonoBehaviour
{
    [Header("Settings")]
    public Transform modelParent;
    public Material destinyDyeMaterial;
    public float modelScale = 1f;
    public Vector3 modelOffset = Vector3.zero;

    [Header("Current Model")]
    public GameObject currentModel;
    public List<Renderer> modelRenderers = new List<Renderer>();

    private GltfImport gltfImport;

    private void Awake()
    {
        if (modelParent == null)
            modelParent = transform;
    }

    /// <summary>
    /// Load a 3D model from file path
    /// </summary>
    public async void LoadModel(string path, Action<bool> onComplete = null)
    {
        Debug.Log($"[CharacterLoader] Loading model: {path}");

        // Clear existing model
        ClearModel();

        if (!File.Exists(path))
        {
            Debug.LogError($"[CharacterLoader] File not found: {path}");
            onComplete?.Invoke(false);
            return;
        }

        string extension = Path.GetExtension(path).ToLower();

        try
        {
            if (extension == ".gltf" || extension == ".glb")
            {
                await LoadGLTF(path);
            }
            else if (extension == ".fbx")
            {
                await LoadFBX(path);
            }
            else
            {
                Debug.LogError($"[CharacterLoader] Unsupported format: {extension}");
                onComplete?.Invoke(false);
                return;
            }

            if (currentModel != null)
            {
                SetupModel();
                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterLoader] Load error: {ex.Message}");
            onComplete?.Invoke(false);
        }
    }

    private async Task LoadGLTF(string path)
    {
        gltfImport = new GltfImport();
        
        bool success = await gltfImport.Load(path);
        
        if (success)
        {
            currentModel = new GameObject("LoadedModel");
            currentModel.transform.SetParent(modelParent);
            await gltfImport.InstantiateMainSceneAsync(currentModel.transform);
            Debug.Log("[CharacterLoader] GLTF loaded successfully");
        }
        else
        {
            Debug.LogError("[CharacterLoader] GLTF load failed");
        }
    }

    private Task LoadFBX(string path)
    {
        // FBX loading requires runtime import plugin or pre-converted assets
        // For now, log a warning and suggest using GLTF
        Debug.LogWarning("[CharacterLoader] FBX runtime loading requires additional setup. Consider converting to GLTF.");
        
        // Placeholder: Try to load from Resources if pre-imported
        string resourceName = Path.GetFileNameWithoutExtension(path);
        GameObject prefab = Resources.Load<GameObject>($"Models/{resourceName}");
        
        if (prefab != null)
        {
            currentModel = Instantiate(prefab, modelParent);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private void SetupModel()
    {
        if (currentModel == null) return;

        // Apply scale and offset
        currentModel.transform.localScale = Vector3.one * modelScale;
        currentModel.transform.localPosition = modelOffset;

        // Center model at origin
        CenterModel();

        // Get all renderers
        modelRenderers.Clear();
        modelRenderers.AddRange(currentModel.GetComponentsInChildren<Renderer>());

        // Apply Destiny Dye material
        if (destinyDyeMaterial != null)
        {
            foreach (var renderer in modelRenderers)
            {
                // Create material instance for each renderer
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material dyeMat = new Material(destinyDyeMaterial);
                    
                    // Copy textures from original material if available
                    if (materials[i] != null)
                    {
                        if (materials[i].HasProperty("_MainTex"))
                            dyeMat.SetTexture("_BaseMap", materials[i].GetTexture("_MainTex"));
                        if (materials[i].HasProperty("_BumpMap"))
                            dyeMat.SetTexture("_NormalMap", materials[i].GetTexture("_BumpMap"));
                    }
                    
                    materials[i] = dyeMat;
                }
                renderer.sharedMaterials = materials;
            }
        }

        Debug.Log($"[CharacterLoader] Model setup complete: {modelRenderers.Count} renderers");
    }

    private void CenterModel()
    {
        Bounds bounds = new Bounds(currentModel.transform.position, Vector3.zero);
        
        foreach (var renderer in currentModel.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Vector3 offset = bounds.center - currentModel.transform.position;
        currentModel.transform.position -= offset;
        
        // Adjust Y to place feet at origin
        float minY = bounds.min.y;
        currentModel.transform.position += Vector3.up * (-minY);
    }

    public void ClearModel()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
        modelRenderers.Clear();
    }

    /// <summary>
    /// Get all materials for dye application
    /// </summary>
    public List<Material> GetAllMaterials()
    {
        List<Material> materials = new List<Material>();
        foreach (var renderer in modelRenderers)
        {
            materials.AddRange(renderer.sharedMaterials);
        }
        return materials;
    }
}
