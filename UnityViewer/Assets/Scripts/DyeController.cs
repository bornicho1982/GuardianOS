using UnityEngine;
using System.Collections.Generic;

public enum DyeType
{
    Primary,
    Secondary,
    Tertiary
}

/// <summary>
/// Controls dye colors on character materials
/// Replicates Destiny 2's dye system
/// </summary>
public class DyeController : MonoBehaviour
{
    [Header("References")]
    public CharacterLoader characterLoader;

    [Header("Shader Properties")]
    public string primaryColorProperty = "_DyePrimary";
    public string secondaryColorProperty = "_DyeSecondary";
    public string tertiaryColorProperty = "_DyeTertiary";
    public string clearCoatProperty = "_ClearCoatStrength";
    public string fresnelProperty = "_FresnelStrength";

    [Header("Default Colors")]
    public Color defaultPrimary = new Color(0.8f, 0.8f, 0.8f);
    public Color defaultSecondary = new Color(0.5f, 0.5f, 0.5f);
    public Color defaultTertiary = new Color(0.3f, 0.3f, 0.3f);

    [Header("Current Dyes")]
    public Dictionary<int, DyeSlot> dyeSlots = new Dictionary<int, DyeSlot>();

    [System.Serializable]
    public class DyeSlot
    {
        public Color primary;
        public Color secondary;
        public Color tertiary;
        public float clearCoat = 0.2f;
        public float fresnel = 0.4f;

        public DyeSlot()
        {
            primary = new Color(0.8f, 0.8f, 0.8f);
            secondary = new Color(0.5f, 0.5f, 0.5f);
            tertiary = new Color(0.3f, 0.3f, 0.3f);
        }
    }

    private void Awake()
    {
        if (characterLoader == null)
            characterLoader = GetComponent<CharacterLoader>();
    }

    /// <summary>
    /// Set a dye color for a specific slot and type
    /// </summary>
    public void SetDye(int slot, DyeType type, Color color)
    {
        if (!dyeSlots.ContainsKey(slot))
            dyeSlots[slot] = new DyeSlot();

        switch (type)
        {
            case DyeType.Primary:
                dyeSlots[slot].primary = color;
                break;
            case DyeType.Secondary:
                dyeSlots[slot].secondary = color;
                break;
            case DyeType.Tertiary:
                dyeSlots[slot].tertiary = color;
                break;
        }

        ApplyDyes();
        Debug.Log($"[DyeController] Set {type} dye for slot {slot}: {color}");
    }

    /// <summary>
    /// Set all dyes for a slot at once
    /// </summary>
    public void SetAllDyes(int slot, Color primary, Color secondary, Color tertiary)
    {
        if (!dyeSlots.ContainsKey(slot))
            dyeSlots[slot] = new DyeSlot();

        dyeSlots[slot].primary = primary;
        dyeSlots[slot].secondary = secondary;
        dyeSlots[slot].tertiary = tertiary;

        ApplyDyes();
    }

    /// <summary>
    /// Set clear coat and fresnel strength
    /// </summary>
    public void SetMaterialProperties(int slot, float clearCoat, float fresnel)
    {
        if (!dyeSlots.ContainsKey(slot))
            dyeSlots[slot] = new DyeSlot();

        dyeSlots[slot].clearCoat = clearCoat;
        dyeSlots[slot].fresnel = fresnel;

        ApplyDyes();
    }

    /// <summary>
    /// Apply all dye colors to materials
    /// </summary>
    public void ApplyDyes()
    {
        if (characterLoader == null) return;

        var materials = characterLoader.GetAllMaterials();
        
        // For now, apply slot 0 dyes to all materials
        // In a full implementation, each armor piece would have its own slot
        DyeSlot slot = dyeSlots.ContainsKey(0) ? dyeSlots[0] : new DyeSlot();

        foreach (var material in materials)
        {
            if (material == null) continue;

            // Apply dye colors
            if (material.HasProperty(primaryColorProperty))
                material.SetColor(primaryColorProperty, slot.primary);
            if (material.HasProperty(secondaryColorProperty))
                material.SetColor(secondaryColorProperty, slot.secondary);
            if (material.HasProperty(tertiaryColorProperty))
                material.SetColor(tertiaryColorProperty, slot.tertiary);

            // Apply material properties
            if (material.HasProperty(clearCoatProperty))
                material.SetFloat(clearCoatProperty, slot.clearCoat);
            if (material.HasProperty(fresnelProperty))
                material.SetFloat(fresnelProperty, slot.fresnel);
        }

        Debug.Log($"[DyeController] Applied dyes to {materials.Count} materials");
    }

    /// <summary>
    /// Reset all dyes to defaults
    /// </summary>
    public void ResetDyes()
    {
        dyeSlots.Clear();
        dyeSlots[0] = new DyeSlot()
        {
            primary = defaultPrimary,
            secondary = defaultSecondary,
            tertiary = defaultTertiary
        };
        ApplyDyes();
    }

    /// <summary>
    /// Convert from Bungie API color format (0-255 or 0-1) to Unity Color
    /// </summary>
    public static Color FromBungieColor(float[] rgba)
    {
        if (rgba == null || rgba.Length < 3) return Color.white;

        // Detect if 0-255 or 0-1 range
        bool needs255 = rgba[0] > 1 || rgba[1] > 1 || rgba[2] > 1;
        
        float r = needs255 ? rgba[0] / 255f : rgba[0];
        float g = needs255 ? rgba[1] / 255f : rgba[1];
        float b = needs255 ? rgba[2] / 255f : rgba[2];
        float a = rgba.Length > 3 ? (needs255 ? rgba[3] / 255f : rgba[3]) : 1f;

        return new Color(r, g, b, a);
    }
}
