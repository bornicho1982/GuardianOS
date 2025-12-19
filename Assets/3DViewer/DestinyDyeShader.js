/**
 * DestinyDyeShader.js - Advanced Destiny 2 Dye System for Three.js
 * 
 * Replicates Bungie's Default Dye System including:
 * - Primary/Secondary/Tertiary dye mixing via dye mask
 * - ORM (AO, Roughness, Metalness) support
 * - Fresnel effect
 * - Clear Coat effect
 * - Specular tint
 * - sRGB encoding and ACES tone mapping compatible
 */

(function () {
    'use strict';

    // ========================================
    // VERTEX SHADER
    // ========================================
    const vertexShader = `
        varying vec2 vUv;
        varying vec3 vNormal;
        varying vec3 vViewDir;
        varying vec3 vWorldPos;

        void main() {
            vUv = uv;
            vec4 worldPos = modelMatrix * vec4(position, 1.0);
            vWorldPos = worldPos.xyz;
            vViewDir = normalize(cameraPosition - worldPos.xyz);
            vNormal = normalize(normalMatrix * normal);
            gl_Position = projectionMatrix * viewMatrix * worldPos;
        }
    `;

    // ========================================
    // FRAGMENT SHADER
    // ========================================
    const fragmentShader = `
        uniform sampler2D albedoMap;
        uniform sampler2D normalMap;
        uniform sampler2D ormMap;
        uniform sampler2D dyeMaskMap;

        uniform vec3 dyePrimary;
        uniform vec3 dyeSecondary;
        uniform vec3 dyeTertiary;
        uniform vec3 specularTint;

        uniform float clearCoatStrength;
        uniform float fresnelStrength;
        uniform float dyeIntensity;
        uniform float metallicBoost;
        uniform float roughnessAdjust;

        uniform bool hasAlbedoMap;
        uniform bool hasNormalMap;
        uniform bool hasOrmMap;
        uniform bool hasDyeMaskMap;

        varying vec2 vUv;
        varying vec3 vNormal;
        varying vec3 vViewDir;
        varying vec3 vWorldPos;

        // Simple lighting calculation
        vec3 calculateLighting(vec3 normal, vec3 viewDir, vec3 baseColor, float roughness, float metalness) {
            // Key light (main)
            vec3 lightDir = normalize(vec3(0.5, 1.0, 0.7));
            vec3 lightColor = vec3(1.0, 0.98, 0.95);
            
            // Diffuse
            float NdotL = max(dot(normal, lightDir), 0.0);
            vec3 diffuse = baseColor * NdotL * lightColor;
            
            // Specular (Blinn-Phong)
            vec3 halfDir = normalize(lightDir + viewDir);
            float specPower = mix(8.0, 256.0, 1.0 - roughness);
            float NdotH = max(dot(normal, halfDir), 0.0);
            float spec = pow(NdotH, specPower) * (1.0 - roughness);
            vec3 specular = spec * lightColor * mix(vec3(0.04), baseColor, metalness);
            
            // Fill light (softer, from left)
            vec3 fillDir = normalize(vec3(-0.7, 0.3, 0.5));
            float fillNdotL = max(dot(normal, fillDir), 0.0);
            vec3 fill = baseColor * fillNdotL * vec3(0.3, 0.35, 0.4) * 0.5;
            
            // Rim light
            vec3 rimDir = normalize(vec3(0.0, 0.5, -1.0));
            float rimNdotL = max(dot(normal, rimDir), 0.0);
            vec3 rim = baseColor * rimNdotL * vec3(0.4, 0.4, 0.5) * 0.3;
            
            // Ambient
            vec3 ambient = baseColor * vec3(0.15, 0.15, 0.18);
            
            return diffuse + specular + fill + rim + ambient;
        }

        void main() {
            // ===== BASE TEXTURES =====
            vec3 albedo = hasAlbedoMap ? texture2D(albedoMap, vUv).rgb : vec3(0.5);
            
            // ORM: R = AO, G = Roughness, B = Metalness
            vec3 orm = hasOrmMap ? texture2D(ormMap, vUv).rgb : vec3(1.0, 0.5, 0.0);
            float ao = orm.r;
            float roughness = clamp(orm.g + roughnessAdjust, 0.0, 1.0);
            float metalness = clamp(orm.b + metallicBoost, 0.0, 1.0);

            // Dye mask: R = primary, G = secondary, B = tertiary
            vec3 dyeMask = hasDyeMaskMap ? texture2D(dyeMaskMap, vUv).rgb : vec3(1.0, 0.0, 0.0);

            // ===== APPLY DYES =====
            // The Destiny 2 formula: multiply albedo by dye colors based on mask channels
            vec3 dyedColor;
            
            if (hasDyeMaskMap) {
                // Use the dye mask to blend colors
                dyedColor = 
                    albedo * dyePrimary * dyeMask.r +
                    albedo * dyeSecondary * dyeMask.g +
                    albedo * dyeTertiary * dyeMask.b;
                
                // Areas not covered by any mask channel get base albedo tinted by primary
                float totalMask = dyeMask.r + dyeMask.g + dyeMask.b;
                if (totalMask < 0.1) {
                    dyedColor = albedo * dyePrimary * 0.8;
                }
            } else {
                // No dye mask - apply primary dye to entire surface
                dyedColor = albedo * dyePrimary * dyeIntensity;
            }

            // ===== NORMAL MAP =====
            vec3 N = vNormal;
            if (hasNormalMap) {
                vec3 normalTex = texture2D(normalMap, vUv).xyz * 2.0 - 1.0;
                // Simple normal blending (proper TBN would be better but this works for most cases)
                N = normalize(vNormal + normalTex * 0.5);
            }
            vec3 V = normalize(vViewDir);

            // ===== FRESNEL =====
            float fresnel = pow(1.0 - max(dot(N, V), 0.0), 5.0) * fresnelStrength;

            // ===== CLEAR COAT =====
            float clearCoat = clearCoatStrength * (1.0 - roughness);

            // ===== SPECULAR TINT =====
            vec3 specular = specularTint * metalness * 0.5;

            // ===== FINAL LIGHTING =====
            vec3 litColor = calculateLighting(N, V, dyedColor, roughness, metalness);
            
            // Apply AO
            litColor *= ao;
            
            // Add specular contributions
            litColor += specular;
            litColor += fresnel * vec3(1.0, 1.0, 1.0);
            litColor += clearCoat * vec3(0.1, 0.1, 0.1);

            // Final color with gamma correction hint (Three.js handles sRGB output)
            gl_FragColor = vec4(litColor, 1.0);
        }
    `;

    /**
     * Create a Destiny Dye ShaderMaterial
     * @param {Object} options - Material options
     * @returns {THREE.ShaderMaterial}
     */
    function createDestinyDyeMaterial(options = {}) {
        const {
            albedoMap = null,
            normalMap = null,
            ormMap = null,
            dyeMaskMap = null,
            dyePrimary = new THREE.Color(0.8, 0.8, 0.8),
            dyeSecondary = new THREE.Color(0.5, 0.5, 0.5),
            dyeTertiary = new THREE.Color(0.3, 0.3, 0.3),
            specularTint = new THREE.Color(1.0, 1.0, 1.0),
            clearCoatStrength = 0.15,
            fresnelStrength = 0.3,
            dyeIntensity = 1.5,
            metallicBoost = 0.0,
            roughnessAdjust = 0.0
        } = options;

        // Ensure proper encoding for textures
        if (albedoMap) {
            albedoMap.encoding = THREE.sRGBEncoding;
        }
        if (normalMap) {
            normalMap.encoding = THREE.LinearEncoding;
        }
        if (ormMap) {
            ormMap.encoding = THREE.LinearEncoding;
        }
        if (dyeMaskMap) {
            dyeMaskMap.encoding = THREE.LinearEncoding;
        }

        const material = new THREE.ShaderMaterial({
            uniforms: {
                albedoMap: { value: albedoMap },
                normalMap: { value: normalMap },
                ormMap: { value: ormMap },
                dyeMaskMap: { value: dyeMaskMap },

                dyePrimary: { value: dyePrimary },
                dyeSecondary: { value: dyeSecondary },
                dyeTertiary: { value: dyeTertiary },
                specularTint: { value: specularTint },

                clearCoatStrength: { value: clearCoatStrength },
                fresnelStrength: { value: fresnelStrength },
                dyeIntensity: { value: dyeIntensity },
                metallicBoost: { value: metallicBoost },
                roughnessAdjust: { value: roughnessAdjust },

                hasAlbedoMap: { value: albedoMap !== null },
                hasNormalMap: { value: normalMap !== null },
                hasOrmMap: { value: ormMap !== null },
                hasDyeMaskMap: { value: dyeMaskMap !== null }
            },
            vertexShader: vertexShader,
            fragmentShader: fragmentShader,
            side: THREE.DoubleSide
        });

        return material;
    }

    /**
     * Apply Destiny Dye material to a mesh or group
     * @param {THREE.Object3D} object - The object to apply materials to
     * @param {Object} dyeColors - Dye color configuration
     * @param {Object} textures - Loaded textures by type
     */
    function applyDestinyDyeToObject(object, dyeColors = {}, textures = {}) {
        const {
            primary = [0.8, 0.8, 0.8],
            secondary = [0.5, 0.5, 0.5],
            tertiary = [0.3, 0.3, 0.3],
            specular = [1.0, 1.0, 1.0]
        } = dyeColors;

        // Normalize colors (Bungie sometimes uses 0-255)
        const normalizeColor = (c) => {
            if (!c || c.length < 3) return [0.8, 0.8, 0.8];
            if (c[0] > 1 || c[1] > 1 || c[2] > 1) {
                return [c[0] / 255, c[1] / 255, c[2] / 255];
            }
            return c;
        };

        const primNorm = normalizeColor(primary);
        const secNorm = normalizeColor(secondary);
        const terNorm = normalizeColor(tertiary);
        const specNorm = normalizeColor(specular);

        object.traverse((child) => {
            if (child.isMesh) {
                const material = createDestinyDyeMaterial({
                    albedoMap: textures.albedo || textures.diffuse || null,
                    normalMap: textures.normal || null,
                    ormMap: textures.orm || textures.gearstack || textures.stack || null,
                    dyeMaskMap: textures.dyemask || textures.dyeslot || null,
                    dyePrimary: new THREE.Color(primNorm[0], primNorm[1], primNorm[2]),
                    dyeSecondary: new THREE.Color(secNorm[0], secNorm[1], secNorm[2]),
                    dyeTertiary: new THREE.Color(terNorm[0], terNorm[1], terNorm[2]),
                    specularTint: new THREE.Color(specNorm[0], specNorm[1], specNorm[2]),
                    clearCoatStrength: 0.2,
                    fresnelStrength: 0.4,
                    dyeIntensity: 1.8
                });

                child.material = material;
                console.log('[DestinyDyeShader] Applied dye material to mesh:', child.name || 'unnamed');
            }
        });
    }

    // Export to global scope
    window.DestinyDyeShader = {
        createMaterial: createDestinyDyeMaterial,
        applyToObject: applyDestinyDyeToObject,
        vertexShader: vertexShader,
        fragmentShader: fragmentShader
    };

    console.log('[DestinyDyeShader] Advanced Destiny 2 Dye System loaded');

})();
