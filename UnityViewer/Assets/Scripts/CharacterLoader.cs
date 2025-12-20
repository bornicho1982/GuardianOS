using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Loads FBX/OBJ models from Resources or AssetBundles
/// For runtime GLTF loading, install glTFast from OpenUPM after setup
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

    private void Awake()
    {
        if (modelParent == null)
            modelParent = transform;
    }

    /// <summary>
    /// Load a 3D model from Resources folder
    /// </summary>
    public void LoadModel(string resourcePath, Action<bool> onComplete = null)
    {
        Debug.Log($"[CharacterLoader] Loading model: {resourcePath}");

        // Clear existing model
        ClearModel();

        try
        {
            // Try to load from Resources
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            
            if (prefab != null)
            {
                currentModel = Instantiate(prefab, modelParent);
                SetupModel();
                onComplete?.Invoke(true);
                Debug.Log("[CharacterLoader] Model loaded from Resources");
            }
            else
            {
                // For runtime loading, we'll need to use AssetBundles or glTFast
                Debug.LogWarning($"[CharacterLoader] Model not found in Resources: {resourcePath}");
                Debug.LogWarning("[CharacterLoader] For runtime file loading, install glTFast from Package Manager");
                onComplete?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterLoader] Load error: {ex.Message}");
            onComplete?.Invoke(false);
        }
    }

    /// <summary>
    /// Load a model from a prefab reference (for editor setup)
    /// </summary>
    public void LoadFromPrefab(GameObject prefab, Action<bool> onComplete = null)
    {
        ClearModel();
        
        if (prefab != null)
        {
            currentModel = Instantiate(prefab, modelParent);
            SetupModel();
            onComplete?.Invoke(true);
        }
        else
        {
            onComplete?.Invoke(false);
        }
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

        // Apply Destiny Dye material if assigned
        if (destinyDyeMaterial != null)
        {
            foreach (var renderer in modelRenderers)
            {
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
