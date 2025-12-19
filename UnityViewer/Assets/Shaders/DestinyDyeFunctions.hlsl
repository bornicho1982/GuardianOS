// Destiny 2 Dye System - Custom HLSL Functions for Shader Graph
// Include this in your Shader Graph using a Custom Function node

#ifndef DESTINY_DYE_INCLUDED
#define DESTINY_DYE_INCLUDED

// sRGB to Linear color space conversion
float3 SRGBToLinear_Destiny(float3 srgb)
{
    return pow(srgb, 2.2);
}

// Linear to sRGB color space conversion
float3 LinearToSRGB_Destiny(float3 linear)
{
    return pow(linear, 1.0 / 2.2);
}

// Destiny 2 Dye Mixing Function
// Combines albedo with primary, secondary, tertiary dyes based on mask channels
void DestinyDyeMix_float(
    float3 Albedo,
    float3 DyeMask,
    float3 DyePrimary,
    float3 DyeSecondary,
    float3 DyeTertiary,
    float DyeIntensity,
    out float3 DyedColor)
{
    // Convert inputs to linear space
    float3 albedoLinear = SRGBToLinear_Destiny(Albedo);
    float3 primaryLinear = SRGBToLinear_Destiny(DyePrimary);
    float3 secondaryLinear = SRGBToLinear_Destiny(DyeSecondary);
    float3 tertiaryLinear = SRGBToLinear_Destiny(DyeTertiary);
    
    // Apply dyes based on mask channels
    // R = Primary, G = Secondary, B = Tertiary
    float3 dyedColor = 
        albedoLinear * primaryLinear * DyeMask.r +
        albedoLinear * secondaryLinear * DyeMask.g +
        albedoLinear * tertiaryLinear * DyeMask.b;
    
    // Fallback for areas without mask coverage
    float totalMask = DyeMask.r + DyeMask.g + DyeMask.b;
    if (totalMask < 0.1)
    {
        dyedColor = albedoLinear * primaryLinear * 0.8;
    }
    
    // Apply intensity multiplier
    dyedColor *= DyeIntensity;
    
    DyedColor = dyedColor;
}

// Fresnel effect calculation
void DestinyFresnel_float(
    float3 Normal,
    float3 ViewDir,
    float Strength,
    out float Fresnel)
{
    float NdotV = saturate(dot(Normal, ViewDir));
    Fresnel = pow(1.0 - NdotV, 5.0) * Strength;
}

// Clear Coat calculation
void DestinyClearCoat_float(
    float Roughness,
    float Strength,
    out float ClearCoat)
{
    ClearCoat = Strength * (1.0 - Roughness);
}

// Full Destiny material calculation
void DestinyMaterial_float(
    float3 Albedo,
    float3 Normal,
    float3 ViewDir,
    float3 DyeMask,
    float3 DyePrimary,
    float3 DyeSecondary,
    float3 DyeTertiary,
    float AO,
    float Roughness,
    float Metalness,
    float ClearCoatStrength,
    float FresnelStrength,
    float DyeIntensity,
    out float3 BaseColor,
    out float FinalRoughness,
    out float FinalMetalness,
    out float FinalAO,
    out float Fresnel,
    out float ClearCoat)
{
    // Calculate dyed color
    DestinyDyeMix_float(
        Albedo, DyeMask,
        DyePrimary, DyeSecondary, DyeTertiary,
        DyeIntensity,
        BaseColor
    );
    
    // Apply AO
    BaseColor *= AO;
    
    // Calculate fresnel
    DestinyFresnel_float(Normal, ViewDir, FresnelStrength, Fresnel);
    
    // Calculate clear coat
    DestinyClearCoat_float(Roughness, ClearCoatStrength, ClearCoat);
    
    // Pass through material properties
    FinalRoughness = Roughness;
    FinalMetalness = Metalness;
    FinalAO = AO;
}

#endif // DESTINY_DYE_INCLUDED
