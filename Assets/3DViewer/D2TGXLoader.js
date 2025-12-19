/**
 * D2TGXLoader - Destiny 2 Model Loader v2
 * Based on lowlidev's TGXLoader - now with proper parsing
 */

(function () {
    'use strict';

    const PROXY_URL = 'http://localhost:5050';

    // Utilities for binary parsing
    const Utils = {
        ubyte: (data, offset) => data[offset],
        byte: (data, offset) => {
            const val = data[offset];
            return val > 127 ? val - 256 : val;
        },
        ushort: (data, offset) => data[offset] | (data[offset + 1] << 8),
        short: (data, offset) => {
            const val = data[offset] | (data[offset + 1] << 8);
            return val > 32767 ? val - 65536 : val;
        },
        uint: (data, offset) => data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24),
        float: (data, offset) => {
            const view = new DataView(new ArrayBuffer(4));
            view.setUint8(0, data[offset]);
            view.setUint8(1, data[offset + 1]);
            view.setUint8(2, data[offset + 2]);
            view.setUint8(3, data[offset + 3]);
            return view.getFloat32(0, true);
        },
        // Half-Float (Float16) to Float32 - CRITICAL for UVs per technical report
        float16: (data, offset) => {
            const h = data[offset] | (data[offset + 1] << 8);
            const s = (h & 0x8000) >> 15; // Sign
            const e = (h & 0x7C00) >> 10; // Exponent
            const f = h & 0x03FF;         // Mantissa

            if (e === 0) {
                // Denormalized number
                return (s ? -1 : 1) * Math.pow(2, -14) * (f / 1024);
            } else if (e === 0x1F) {
                // Infinity or NaN
                return f ? NaN : (s ? -Infinity : Infinity);
            }
            // Normalized number
            return (s ? -1 : 1) * Math.pow(2, e - 15) * (1 + f / 1024);
        },
        string: (data, offset, length) => {
            let str = '';
            for (let i = 0; i < length; i++) {
                const chr = data[offset + i];
                if (chr === 0) break;
                str += String.fromCharCode(chr);
            }
            return str;
        },
        // Normalize signed value to -1 to 1 (SNORM)
        normalize: (value, bits) => {
            const max = Math.pow(2, bits - 1) - 1;
            return Math.max(value / max, -1);
        },
        // Normalize unsigned value to 0 to 1 (UNORM)
        unormalize: (value, bits) => {
            const max = Math.pow(2, bits) - 1;
            return value / max;
        },
        // For Int8 normals: divide by 127 NOT 255 (per technical report)
        normalByte: (value) => value / 127.0
    };

    class D2TGXLoader {
        constructor() {
            this.proxyUrl = PROXY_URL;
            this.textureLoader = new THREE.TextureLoader();
            this.loadedTextures = {};
        }

        extractGearDyes(contentEntry) {
            // Organize dyes by slot_type_index for proper color assignment per part
            // Each mesh part has a gear_dye_change_color_index that maps to a slot

            const dyesBySlot = {};

            // Helper to add dye to slot
            const addDyeToSlot = (dye, dyeType) => {
                if (!dye) return;
                const parsed = this.parseDyeData(dye);
                const slot = parsed.slotTypeIndex || 0;

                if (!dyesBySlot[slot]) {
                    dyesBySlot[slot] = { primaryColor: null, secondaryColor: null, wornColor: null };
                }

                // For custom_dyes (shaders), these override default_dyes
                if (dyeType === 'custom' || !dyesBySlot[slot].primaryColor) {
                    dyesBySlot[slot].primaryColor = parsed.primaryColor;
                    dyesBySlot[slot].secondaryColor = parsed.secondaryColor;
                    dyesBySlot[slot].wornColor = parsed.wornColor;
                    dyesBySlot[slot].primaryParams = parsed.primaryParams;
                    dyesBySlot[slot].secondaryParams = parsed.secondaryParams;
                }
            };

            // Parse default dyes first
            if (contentEntry.default_dyes) {
                for (const dye of contentEntry.default_dyes) {
                    addDyeToSlot(dye, 'default');
                }
            }

            // Parse custom dyes (shader colors) - these override defaults
            if (contentEntry.custom_dyes) {
                for (const dye of contentEntry.custom_dyes) {
                    addDyeToSlot(dye, 'custom');
                }
            }

            // Parse locked dyes (exotic items) - highest priority
            if (contentEntry.locked_dyes) {
                for (const dye of contentEntry.locked_dyes) {
                    addDyeToSlot(dye, 'locked');
                }
            }

            console.log('[D2TGXLoader] Extracted dyes by slot:', dyesBySlot);
            return dyesBySlot;
        }

        // Parse individual dye data
        parseDyeData(dye) {
            if (!dye || !dye.material_properties) {
                return {
                    primaryColor: [0.5, 0.5, 0.5],
                    secondaryColor: [0.3, 0.3, 0.3],
                    wornColor: [0.2, 0.2, 0.2]
                };
            }

            const props = dye.material_properties;
            // Helper to normalize color from 0-255 to 0-1
            const normalizeColor = (color) => {
                if (!color) return null;
                // If values are > 1, assume 0-255 range
                if (color[0] > 1 || color[1] > 1 || color[2] > 1) {
                    return [color[0] / 255, color[1] / 255, color[2] / 255, color[3] !== undefined ? color[3] / 255 : 1];
                }
                return color;
            };

            return {
                slotTypeIndex: dye.slot_type_index || 0,
                primaryColor: normalizeColor(props.primary_albedo_tint) || [0.5, 0.5, 0.5],
                secondaryColor: normalizeColor(props.secondary_albedo_tint) || [0.3, 0.3, 0.3],
                wornColor: normalizeColor(props.worn_albedo_tint) || [0.2, 0.2, 0.2],
                primaryParams: props.primary_material_params || [0, 0, 0, 0],
                secondaryParams: props.secondary_material_params || [0, 0, 0, 0]
            };
        }

        // Extract texture URLs from content entry - actual structure
        extractTextures(contentEntry, isFemale) {
            const result = {
                diffuse: [],
                gearstack: [],
                normal: []
            };

            const allTextures = contentEntry.textures || [];

            // Get texture indices based on gender
            let textureIndices = [];
            if (isFemale && contentEntry.female_index_set) {
                textureIndices = contentEntry.female_index_set.textures || [];
            } else if (contentEntry.male_index_set) {
                textureIndices = contentEntry.male_index_set.textures || [];
            }

            // Get dye texture indices
            const dyeIndices = contentEntry.dye_index_set?.textures || [];

            console.log('[D2TGXLoader] Texture indices:', textureIndices);
            console.log('[D2TGXLoader] Dye indices:', dyeIndices);
            console.log('[D2TGXLoader] All textures:', allTextures.length);

            // Map indices to actual texture filenames
            for (const idx of textureIndices) {
                if (idx < allTextures.length) {
                    const texName = allTextures[idx];
                    console.log(`[D2TGXLoader] Texture at index ${idx}: ${texName}`);
                    result.diffuse.push(texName);
                }
            }

            for (const idx of dyeIndices) {
                if (idx < allTextures.length) {
                    console.log(`[D2TGXLoader] Dye Texture at index ${idx}: ${allTextures[idx]}`);
                }
            }

            console.log('[D2TGXLoader] Extracted texture paths:', result);
            return result;
        }

        // Load a texture via the proxy
        async loadTexture(texturePath) {
            if (!texturePath) return null;

            // Check cache
            if (this.loadedTextures[texturePath]) {
                return this.loadedTextures[texturePath];
            }

            try {
                const url = `${this.proxyUrl}/api/texture/${texturePath}`;
                const texture = await new Promise((resolve, reject) => {
                    this.textureLoader.load(url, resolve, undefined, reject);
                });
                texture.flipY = false;  // TGX textures don't need flip
                this.loadedTextures[texturePath] = texture;
                return texture;
            } catch (error) {
                console.warn('[D2TGXLoader] Failed to load texture:', texturePath, error);
                return null;
            }
        }

        // Load texture from TGXM bin file - these are containers with PNG/JPEG inside
        async loadTextureFromTGXM(filename) {
            // Check cache
            if (this.loadedTextures[filename]) {
                return this.loadedTextures[filename];
            }

            const url = `${this.proxyUrl}/api/geometry/platform/mobile/textures/${filename}`;
            console.log('[D2TGXLoader] Loading texture TGXM:', url);

            try {
                const response = await fetch(url);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);

                const buffer = await response.arrayBuffer();
                const data = new Uint8Array(buffer);

                // Parse TGXM header
                const magic = Utils.string(data, 0, 4);
                if (magic !== 'TGXM') {
                    console.warn('[D2TGXLoader] Invalid texture TGXM:', magic);
                    return null;
                }

                const fileOffset = Utils.uint(data, 8);
                const fileCount = Utils.uint(data, 12);

                console.log('[D2TGXLoader] Texture TGXM has', fileCount, 'files');

                // Parse files and find images
                const textures = [];
                for (let i = 0; i < fileCount; i++) {
                    const headerOffset = fileOffset + (0x110 * i);
                    const name = Utils.string(data, headerOffset, 256);
                    const offset = Utils.uint(data, headerOffset + 0x100);
                    const size = Utils.uint(data, headerOffset + 0x108);

                    const fileData = data.slice(offset, offset + size);

                    // Check if this is a PNG or JPEG
                    const isPng = Utils.string(fileData, 1, 3) === 'PNG';
                    const isJpeg = fileData[0] === 0xFF && fileData[1] === 0xD8;

                    if (isPng || isJpeg) {
                        const mimeType = isPng ? 'image/png' : 'image/jpeg';
                        const blob = new Blob([fileData], { type: mimeType });
                        const imageUrl = URL.createObjectURL(blob);

                        // Create THREE.js texture
                        const texture = await new Promise((resolve, reject) => {
                            const image = new Image();
                            image.onload = () => {
                                const tex = new THREE.Texture(image);
                                tex.needsUpdate = true;
                                tex.flipY = false;
                                tex.wrapS = THREE.RepeatWrapping;
                                tex.wrapT = THREE.RepeatWrapping;
                                resolve(tex);
                            };
                            image.onerror = reject;
                            image.src = imageUrl;
                        });

                        texture.name = name;
                        textures.push({ name, texture, isPng });

                        // CACHE BY NAME SO WE CAN FIND IT LATER
                        // This fixes the issue where only the first texture was accessible via the filename hash
                        if (name) {
                            this.loadedTextures[name] = texture;
                        }

                        console.log('[D2TGXLoader] Loaded texture image:', name, isPng ? 'PNG' : 'JPEG');
                    }
                }

                // Cache and return first texture (usually diffuse) for legacy lookup by container hash
                if (textures.length > 0) {
                    this.loadedTextures[filename] = textures[0].texture;
                    return textures[0].texture;
                }

                return null;
            } catch (error) {
                console.warn('[D2TGXLoader] Failed to load texture TGXM:', filename, error);
                return null;
            }
        }

        async load(config) {
            const {
                itemHashes = [],
                shaderHashes = [],
                ornamentHashes = [],
                isFemale = false,
                classType = 1,
                onProgress = () => { },
                onError = console.error
            } = config;

            console.log('[D2TGXLoader] Loading items:', itemHashes);
            console.log('[D2TGXLoader] With shaders:', shaderHashes);
            console.log('[D2TGXLoader] With ornaments:', ornamentHashes);
            onProgress({ message: 'Cargando datos del manifest...' });

            try {
                const meshes = [];

                for (let i = 0; i < itemHashes.length; i++) {
                    const itemHash = itemHashes[i];
                    const shaderHash = shaderHashes[i] || 0;
                    const ornamentHash = ornamentHashes[i] || 0;

                    onProgress({ message: `Cargando item ${itemHash}...` });

                    // If ornament is equipped, try to load ornament geometry instead
                    let gearAsset = null;
                    if (ornamentHash > 0) {
                        console.log('[D2TGXLoader] Trying ornament geometry for:', ornamentHash);
                        gearAsset = await this.getGearAsset(ornamentHash);
                        if (gearAsset) {
                            console.log('[D2TGXLoader] ✓ Using ORNAMENT geometry:', ornamentHash);
                        } else {
                            console.log('[D2TGXLoader] Ornament has no gear asset, falling back to base');
                        }
                    }

                    // Fall back to base item geometry
                    if (!gearAsset) {
                        gearAsset = await this.getGearAsset(itemHash);
                    }

                    if (!gearAsset) {
                        console.warn('[D2TGXLoader] No gear asset for', itemHash);
                        continue;
                    }

                    // If we have a shader hash, load dyes from shader instead of gear
                    onProgress({ message: 'Cargando colores de shader...' });
                    let dyeColors = null;

                    if (shaderHash > 0) {
                        console.log('[D2TGXLoader] Loading shader dyes for hash:', shaderHash);

                        // Try to get shader gear asset from local proxy first
                        const shaderAsset = await this.getGearAsset(shaderHash);
                        if (shaderAsset) {
                            dyeColors = await this.loadGearDyeData(shaderAsset);
                            console.log('[D2TGXLoader] Got shader dyes from local:', dyeColors);
                        }

                        // Fallback: try lowlidev API which has shader gear JSONs
                        if (!dyeColors || Object.keys(dyeColors).length === 0) {
                            dyeColors = await this.loadShaderFromLowlidev(shaderHash);
                        }
                    }

                    // Fallback to gear default dyes if no shader dyes
                    if (!dyeColors || Object.keys(dyeColors).length === 0) {
                        dyeColors = await this.loadGearDyeData(gearAsset);
                    }

                    const mesh = await this.loadItemGeometry(gearAsset, isFemale, onProgress, dyeColors);
                    if (mesh) meshes.push(mesh);
                }

                if (meshes.length === 0) {
                    throw new Error('No meshes loaded');
                }

                const group = new THREE.Group();
                meshes.forEach(mesh => group.add(mesh));

                console.log('[D2TGXLoader] Loaded', meshes.length, 'items');
                return group;

            } catch (error) {
                console.error('[D2TGXLoader] Load failed:', error);
                onError(error);
                throw error;
            }
        }

        async getGearAsset(itemHash) {
            const url = `${this.proxyUrl}/api/gearasset/${itemHash}`;
            try {
                const response = await fetch(url);
                const data = await response.json();
                if (data.ErrorCode === 1 && data.Response && data.Response.gearAsset) {
                    console.log('[D2TGXLoader] Got gear asset for', itemHash);
                    return data.Response.gearAsset;
                }
                return null;
            } catch (error) {
                console.error('[D2TGXLoader] Failed to get gear asset:', error);
                return null;
            }
        }

        // Load shader colors from DestinyInventoryItemDefinition
        // According to research: Colors are in material_properties of the shader's item definition
        async loadShaderFromLowlidev(shaderHash) {
            try {
                // Query DestinyInventoryItemDefinition for the shader
                const url = `${this.proxyUrl}/api/shader/${shaderHash}`;
                console.log('[D2TGXLoader] Fetching shader definition:', shaderHash);

                const response = await fetch(url);
                if (!response.ok) {
                    console.warn('[D2TGXLoader] Shader definition API failed:', response.status);
                    return null;
                }

                const data = await response.json();
                console.log('[D2TGXLoader] Shader definition response keys:',
                    data.Response ? Object.keys(data.Response) : 'no response');

                if (data.ErrorCode === 1 && data.Response) {
                    const shaderDef = data.Response;

                    // Log the full structure for debugging
                    console.log('[D2TGXLoader] Shader definition:', JSON.stringify(shaderDef).substring(0, 500));

                    // The shader's color data should be in translucencyBlock or preview properties
                    // Or in the plug.preview section
                    const dyeColors = this.extractShaderColorsFromDefinition(shaderDef);

                    if (dyeColors && Object.keys(dyeColors).length > 0) {
                        console.log('[D2TGXLoader] ✅ Extracted shader colors!', dyeColors);
                        return dyeColors;
                    } else {
                        console.log('[D2TGXLoader] No colors found in shader definition, checking preview...');

                        // Some shaders have preview.previewVendorHash or other preview data
                        if (shaderDef.preview) {
                            console.log('[D2TGXLoader] Shader preview:', shaderDef.preview);
                        }
                        if (shaderDef.plug) {
                            console.log('[D2TGXLoader] Shader plug:', shaderDef.plug);
                        }
                        if (shaderDef.translucencyBlock) {
                            console.log('[D2TGXLoader] Shader translucencyBlock:', shaderDef.translucencyBlock);
                        }
                    }
                }

                return null;
            } catch (error) {
                console.warn('[D2TGXLoader] Error loading shader definition:', error);
                return null;
            }
        }

        // Extract color data from shader's DestinyInventoryItemDefinition
        extractShaderColorsFromDefinition(shaderDef) {
            const dyeColors = {};

            // Try various known locations for shader color data
            // Based on research: material_properties, translucencyBlock, preview

            // Check for translucencyBlock (some shaders store color data here)
            if (shaderDef.translucencyBlock) {
                const tb = shaderDef.translucencyBlock;
                console.log('[D2TGXLoader] Found translucencyBlock:', tb);
                // Could contain color hints
            }

            // Check plug.preview for color information
            if (shaderDef.plug && shaderDef.plug.preview) {
                console.log('[D2TGXLoader] Found plug.preview:', shaderDef.plug.preview);
            }

            // Check if there are any color-related properties directly on the item
            // Look for common color property names
            const colorProps = ['backgroundColor', 'foregroundColor', 'secondaryColor',
                'tertiaryColor', 'colorMaterial', 'materialProperties'];

            for (const prop of colorProps) {
                if (shaderDef[prop]) {
                    console.log(`[D2TGXLoader] Found ${prop}:`, shaderDef[prop]);
                }
            }

            // Check displayProperties for any color hints
            if (shaderDef.displayProperties) {
                console.log('[D2TGXLoader] Shader display:', shaderDef.displayProperties.name);
            }

            // Check for investment stats or perks that might contain color data
            if (shaderDef.investmentStats) {
                console.log('[D2TGXLoader] Investment stats count:', shaderDef.investmentStats.length);
            }

            // The actual color data might be in a gearDyes array if present
            if (shaderDef.gearDyes) {
                console.log('[D2TGXLoader] Found gearDyes!');
                return this.extractDyeColors({ custom_dyes: shaderDef.gearDyes });
            }

            // Some shaders reference a "preview" item that contains the actual colors
            // This might require a second API call
            if (shaderDef.preview && shaderDef.preview.previewVendorHash) {
                console.log('[D2TGXLoader] Shader has previewVendorHash:', shaderDef.preview.previewVendorHash);
            }

            return dyeColors;
        }


        // Load gear JSON with dye color data
        async loadGearDyeData(gearAsset) {
            // The gear references are in gearAsset.gear array
            const gearRefs = gearAsset.gear || [];
            if (gearRefs.length === 0) return null;

            // Get the first gear reference hash
            const gearHash = gearRefs[0];
            if (!gearHash) return null;

            // Remove .js extension if present
            const cleanHash = gearHash.replace('.js', '');

            try {
                const url = `${this.proxyUrl}/api/gear/${cleanHash}`;
                console.log('[D2TGXLoader] Loading gear dye data:', url);

                const response = await fetch(url);
                if (!response.ok) {
                    console.warn('[D2TGXLoader] Gear JSON not found:', cleanHash);
                    return null;
                }

                const gearData = await response.json();
                console.log('[D2TGXLoader] Got gear data keys:', Object.keys(gearData));

                // Extract dye colors from gear data
                return this.extractDyeColors(gearData);
            } catch (error) {
                console.warn('[D2TGXLoader] Failed to load gear dye data:', error);
                return null;
            }
        }

        // Extract dye colors from gear JSON data
        extractDyeColors(gearData) {
            const dyes = {};
            const dyeOrder = ['default_dyes', 'custom_dyes', 'locked_dyes'];

            let foundAny = false;
            for (const dyeType of dyeOrder) {
                const dyeArray = gearData[dyeType] || [];
                console.log(`[D2TGXLoader] ${dyeType}: ${dyeArray.length} dyes`);

                for (const dye of dyeArray) {
                    const slotIndex = dye.slot_type_index !== undefined ? dye.slot_type_index : 0;
                    const matProps = dye.material_properties;

                    if (matProps) {
                        // Try different property names used by Destiny/Destiny2
                        let primary = matProps.primary_albedo_tint ||
                            matProps.primary_color ||
                            matProps.primary_diffuse_tint || null;
                        let secondary = matProps.secondary_albedo_tint ||
                            matProps.secondary_color ||
                            matProps.secondary_diffuse_tint || null;
                        let worn = matProps.worn_albedo_tint ||
                            matProps.worn_color || null;

                        if (primary || secondary) {
                            dyes[slotIndex] = {
                                primaryColor: primary || [0.5, 0.5, 0.5],
                                secondaryColor: secondary || [0.4, 0.4, 0.4],
                                wornColor: worn || [0.3, 0.3, 0.3]
                            };
                            console.log('[D2TGXLoader] Dye slot', slotIndex,
                                'primary:', dyes[slotIndex].primaryColor,
                                'secondary:', dyes[slotIndex].secondaryColor);
                            foundAny = true;
                        }
                    }
                }
            }

            if (!foundAny) {
                console.warn('[D2TGXLoader] No dye colors found in gear data');
            }

            return dyes;
        }

        async loadItemGeometry(gearAsset, isFemale, onProgress, dyeColors = null) {
            const content = gearAsset.content;
            if (!content || content.length === 0) return null;

            const contentEntry = content[0];
            const geometryFiles = contentEntry.geometry || [];
            if (geometryFiles.length === 0) return null;

            // Extract dyes and textures from this item
            const gearDyes = this.extractGearDyes(contentEntry);
            const texturePaths = this.extractTextures(contentEntry, isFemale);

            // Use dyeColors from gear JSON if available, otherwise use empty
            const resolvedDyeColors = dyeColors || {};
            console.log('[D2TGXLoader] Using dye colors:', resolvedDyeColors);

            // Try to load textures for this item
            // Try to load textures for this item
            let loadedTexture = null;
            if (texturePaths.diffuse && texturePaths.diffuse.length > 0) {
                // DEBUG: Load ALL textures to see their internal names
                for (let i = 0; i < texturePaths.diffuse.length; i++) {
                    const textureFile = texturePaths.diffuse[i];
                    console.log(`[D2TGXLoader] Inspecting diffuse texture [${i}]: ${textureFile} ...`);
                    try {
                        // Load it (this triggers the internallog with the real filename)
                        const tempTex = await this.loadTextureFromTGXM(textureFile);
                        if (i === 0) loadedTexture = tempTex; // Keep the first one as main for now
                    } catch (e) {
                        console.warn('[D2TGXLoader] Inspection failed:', e);
                    }
                }
            }

            // Determine geometry indices
            let geometryIndices = [];
            if (contentEntry.male_index_set && !isFemale) {
                geometryIndices = contentEntry.male_index_set.geometry || [];
            } else if (contentEntry.female_index_set && isFemale) {
                geometryIndices = contentEntry.female_index_set.geometry || [];
            } else if (contentEntry.region_index_sets) {
                for (const key in contentEntry.region_index_sets) {
                    const sets = contentEntry.region_index_sets[key];
                    if (sets && sets.length > 0 && sets[0].geometry) {
                        geometryIndices = sets[0].geometry;
                        break;
                    }
                }
            }

            if (geometryIndices.length === 0) {
                geometryIndices = geometryFiles.map((_, i) => i);
            }

            console.log('[D2TGXLoader] Loading geometry indices:', geometryIndices);
            onProgress({ message: 'Cargando geometría...' });

            const meshes = [];

            for (const index of geometryIndices) {
                if (index < 0 || index >= geometryFiles.length) continue;
                const geometryFile = geometryFiles[index];
                try {
                    const mesh = await this.loadTGXM(geometryFile, onProgress, resolvedDyeColors, texturePaths, loadedTexture, this.loadedTextures);
                    if (mesh) meshes.push(mesh);
                } catch (error) {
                    console.warn('[D2TGXLoader] Failed to load geometry:', geometryFile, error);
                }
            }

            if (meshes.length === 0) return null;

            const group = new THREE.Group();
            meshes.forEach(m => group.add(m));
            return group;
        }

        // Resolve gear dyes based on priority
        resolveGearDyes(gearDyes) {
            const resolved = {};
            const dyeOrder = ['default', 'custom', 'locked'];

            for (const dyeType of dyeOrder) {
                const dyes = gearDyes[dyeType] || [];
                for (const dye of dyes) {
                    const slot = dye.slotTypeIndex || 0;
                    // Later dyes override earlier ones at same slot
                    resolved[slot] = dye;
                }
            }

            return resolved;
        }

        async loadTGXM(filename, onProgress, gearDyes = {}, texturePaths = {}, loadedTexture = null, loadedTextures = {}) {
            let cleanName = filename;
            if (cleanName.endsWith('.bin')) cleanName = cleanName.slice(0, -4);

            const url = `${this.proxyUrl}/api/geometry/platform/mobile/geometry/${cleanName}`;
            console.log('[D2TGXLoader] Loading TGXM:', url);

            try {
                const response = await fetch(url);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);

                const buffer = await response.arrayBuffer();
                const data = new Uint8Array(buffer);

                return this.parseTGXM(data, cleanName, gearDyes, texturePaths, loadedTexture, loadedTextures);

            } catch (error) {
                console.error('[D2TGXLoader] Failed to load TGXM:', error);
                return null;
            }
        }

        parseTGXM(data, filename, gearDyes = {}, texturePaths = {}, loadedTexture = null, loadedTextures = {}) {
            const magic = Utils.string(data, 0, 4);
            if (magic !== 'TGXM') {
                console.error('[D2TGXLoader] Invalid TGXM magic:', magic);
                return null;
            }

            const version = Utils.uint(data, 4);
            const fileOffset = Utils.uint(data, 8);
            const fileCount = Utils.uint(data, 12);
            const identifier = Utils.string(data, 16, 256);

            // Parse file table and create lookup
            const files = {};
            const lookup = [];

            for (let i = 0; i < fileCount; i++) {
                const headerOffset = fileOffset + (0x110 * i);
                const name = Utils.string(data, headerOffset, 256);
                const offset = Utils.uint(data, headerOffset + 0x100);
                const type = Utils.uint(data, headerOffset + 0x104);
                const size = Utils.uint(data, headerOffset + 0x108);

                lookup.push(name);
                files[name] = {
                    name,
                    offset,
                    type,
                    size,
                    data: data.slice(offset, offset + size)
                };
            }

            console.log('[D2TGXLoader] Files in TGXM:', lookup);

            // Find render_metadata.js
            let metadata = null;
            for (const name of lookup) {
                if (name.includes('.js') || name.includes('render_metadata')) {
                    try {
                        const jsonStr = Utils.string(files[name].data, 0, files[name].size);
                        metadata = JSON.parse(jsonStr);
                    } catch (e) {
                        console.warn('[D2TGXLoader] Failed to parse metadata:', e);
                    }
                }
            }

            if (!metadata || !metadata.render_model) {
                console.warn('[D2TGXLoader] No render metadata');
                return null;
            }

            // Parse geometry using proper TGX format
            return this.parseTGXAsset({ metadata, files, lookup }, filename, gearDyes, texturePaths, loadedTexture, loadedTextures);
        }

        // Based on lowlidev's parseTGXAsset
        parseTGXAsset(tgxBin, geometryHash, gearDyes = {}, texturePaths = {}, loadedTexture = null, loadedTextures = {}) {
            const metadata = tgxBin.metadata;
            const renderMeshes = metadata.render_model.render_meshes;

            if (!renderMeshes || renderMeshes.length === 0) {
                console.warn('[D2TGXLoader] No render meshes');
                return null;
            }

            const group = new THREE.Group();

            for (let m = 0; m < renderMeshes.length; m++) {
                const renderMesh = renderMeshes[m];

                // Parse IndexBuffer
                const indexBufferInfo = renderMesh.index_buffer;
                const indexFileName = indexBufferInfo.file_name;
                const indexFile = tgxBin.files[indexFileName];

                if (!indexFile) {
                    console.warn('[D2TGXLoader] Missing index file:', indexFileName);
                    continue;
                }

                const indexBufferData = indexFile.data;
                const indexBuffer = [];
                const valueByteSize = indexBufferInfo.value_byte_size || 2;

                for (let j = 0; j < indexBufferInfo.byte_size; j += valueByteSize) {
                    indexBuffer.push(Utils.ushort(indexBufferData, j));
                }

                console.log('[D2TGXLoader] IndexBuffer:', indexBuffer.length, 'indices');

                // Parse VertexBuffers
                const vertexBuffer = this.parseVertexBuffers(tgxBin, renderMesh);
                if (vertexBuffer.length === 0) {
                    console.warn('[D2TGXLoader] Empty vertex buffer');
                    continue;
                }

                // Get position transform (only used if positions are normalized shorts)
                const positionOffset = renderMesh.position_offset || [0, 0, 0];
                const positionScale = renderMesh.position_scale || [1, 1, 1];
                const texcoordOffset = renderMesh.texcoord_offset || [0, 0];
                const texcoordScale = renderMesh.texcoord_scale || [1, 1];

                // Postion format is float4 (not normalized) - no scaling needed
                // Scale would only be needed for short4n normalized positions
                console.log(`[D2TGXLoader] UV Transform - Scale: ${texcoordScale[0].toFixed(4)}, ${texcoordScale[1].toFixed(4)} Offset: ${texcoordOffset[0].toFixed(4)}, ${texcoordOffset[1].toFixed(4)}`);
                console.log('[D2TGXLoader] VertexBuffer:', vertexBuffer.length, 'vertices');

                // Get stage parts to render
                const stagePartList = renderMesh.stage_part_list || [];
                const stagePartOffsets = renderMesh.stage_part_offsets || [];
                const partLimit = stagePartOffsets[4] || stagePartList.length;

                // Create geometry
                const geometry = new THREE.BufferGeometry();
                const positions = [];
                const normals = [];
                const uvs = [];
                const indices = [];

                // Add all vertices with smart position transform
                for (let v = 0; v < vertexBuffer.length; v++) {
                    const vertex = vertexBuffer[v];
                    const pos = vertex.position0 || [0, 0, 0, 1];
                    const norm = vertex.normal0 || [0, 1, 0];
                    const uv = vertex.texcoord0 || [0, 0];

                    // Log first vertex to debug coordinate order
                    if (v === 0) {
                        console.log('[D2TGXLoader] Sample vertex position values:',
                            'X:', pos[0]?.toFixed(4),
                            'Y:', pos[1]?.toFixed(4),
                            'Z:', pos[2]?.toFixed(4),
                            'W:', pos[3]?.toFixed(4));
                    }

                    // Destiny uses a different coordinate system
                    // Use original XYZ - viewer will handle orientation via rotation
                    positions.push(pos[0], pos[1], pos[2]);

                    // Normals as-is
                    normals.push(norm[0], norm[1], norm[2]);

                    // Apply texcoord transform for UVs
                    const finalU = uv[0] * texcoordScale[0] + texcoordOffset[0];
                    const finalV = uv[1] * texcoordScale[1] + texcoordOffset[1];

                    if (v < 8) {
                        console.log(`[D2TGXLoader] Vertex ${v} Final UV: ${finalU.toFixed(4)}, ${finalV.toFixed(4)} (Raw: ${uv[0]}, ${uv[1]})`);
                    }

                    uvs.push(finalU, finalV);
                }

                // =========================================================
                // PER-PART MATERIALS IMPLEMENTATION (Geometry Groups)
                // =========================================================

                // 1. Create Material Palette
                // We create a palette of materials for 4 dye slots (0-3)
                // Each slot has 2 variants: Primary and Secondary
                // 1. DYNAMIC MATERIAL SYSTEM
                // Instead of a fixed palette, we will create materials on demand.
                const materials = [];
                const materialMap = new Map(); // Key -> Index

                // Helper to get or create a material
                const getMaterialIndex = (part) => {
                    const dyeIndex = part.gear_dye_change_color_index || 0;

                    // Determine slot/variant logic (0-7 mapping)
                    let slot = 0;
                    let isPrimary = true;
                    // Simplified mapping: 0,1->Slot0; 2,3->Slot1; 4,5->Slot2; 6,7->Slot3
                    slot = Math.floor(dyeIndex / 2);
                    isPrimary = (dyeIndex % 2) === 0;

                    // Resolve color
                    let color = [0.5, 0.5, 0.5];
                    if (gearDyes && gearDyes[slot]) {
                        const d = gearDyes[slot];
                        const c = isPrimary ? d.primaryColor : d.secondaryColor;
                        if (c) color = c;
                    }

                    // Resolve Textures from 'static_textures'
                    let diffuseTex = null;
                    let normalTex = null;
                    let stackTex = null;

                    if (part.shader && part.shader.static_textures) {
                        for (const texId of part.shader.static_textures) {
                            if (!texId) continue;
                            const lowerId = texId.toLowerCase();

                            // Search through LOADED TEXTURE VALUES by their .name property
                            // loadedTextures keys are hashes, but values are THREE.Texture objects with .name set to internal ID
                            const matchingTexture = Object.values(loadedTextures).find(tex => tex.name && tex.name.includes(texId));

                            if (matchingTexture) {
                                if (lowerId.includes('diffuse') || lowerId.includes('dif') || lowerId.includes('gbit') || lowerId.includes('albedo')) {
                                    diffuseTex = matchingTexture;
                                } else if (lowerId.includes('normal') || lowerId.includes('norm') || lowerId.includes('cn')) {
                                    normalTex = matchingTexture;
                                } else if (lowerId.includes('stack') || lowerId.includes('mrc') || lowerId.includes('gs') || lowerId.includes('msk')) {
                                    stackTex = matchingTexture;
                                    console.log(`[D2TGXLoader] Found STACK Tex: ${stackTex.name} for ID: ${texId}`);
                                }
                            }
                        }
                    }

                    // Fallback
                    if (!diffuseTex && loadedTexture) diffuseTex = loadedTexture;

                    // Unique Key
                    const diffUuid = diffuseTex ? diffuseTex.uuid : 'null';
                    const normUuid = normalTex ? normalTex.uuid : 'null';
                    const stackUuid = stackTex ? stackTex.uuid : 'null';
                    const matKey = `d${dyeIndex}_c${color.join('-')}_dt${diffUuid}_nt${normUuid}_st${stackUuid}`;

                    if (materialMap.has(matKey)) return materialMap.get(matKey);

                    // Set correct encoding for textures (Three.js r128 uses .encoding)
                    if (diffuseTex) diffuseTex.encoding = THREE.sRGBEncoding;
                    if (normalTex) normalTex.encoding = THREE.LinearEncoding;
                    if (stackTex) stackTex.encoding = THREE.LinearEncoding;

                    const mat = new THREE.MeshStandardMaterial({
                        color: new THREE.Color(1, 1, 1),
                        map: diffuseTex || loadedTexture, // Diffuse is base (sRGB)
                        normalMap: normalTex,              // Normal map (Linear)
                        roughnessMap: stackTex,            // ORM G channel (Standard Three.js handles it)
                        metalnessMap: stackTex,            // ORM B channel (Standard Three.js handles it)
                        aoMap: stackTex,                   // ORM R channel (requires UV2)
                        aoMapIntensity: 1.0,               // Ensure full intensity
                        roughness: 1.0,                    // Base values, modulated by maps
                        metalness: 1.0,
                        side: THREE.DoubleSide
                    });

                    mat.needsUpdate = true; // Refresh material after texture assignment

                    // INJECT CUSTOM SHADER FOR DYE MASKING (Applied to ALL materials for debugging)
                    // This uses the 'stackTex' (MRC texture) to mix dye colors
                    // Red Channel -> Primary Color
                    // Green Channel -> Secondary Color
                    // Blue Channel -> (Usually emission or detail, ignored for now)
                    {
                        // NORMALIZE colors - Bungie sometimes sends 0-255, we need 0-1
                        const normalizeColor = (c) => {
                            if (!c || c.length < 3) return [1, 1, 1];
                            // If any value > 1, assume it's 0-255 range
                            const needsNormalize = c.some(v => v > 1);
                            if (needsNormalize) {
                                return c.map(v => Math.min(1, v / 255));
                            }
                            return c;
                        };

                        // Store dye colors in userdata for shader access
                        const dyeData = (gearDyes && gearDyes[slot]) ? gearDyes[slot] : null;

                        let prim = [1, 1, 1];  // Default white
                        let sec = [0.5, 0.5, 0.5];  // Default gray

                        if (dyeData) {
                            if (dyeData.primaryColor) {
                                prim = normalizeColor(dyeData.primaryColor);
                            }
                            if (dyeData.secondaryColor) {
                                sec = normalizeColor(dyeData.secondaryColor);
                            }
                            console.log(`[D2TGXLoader] Material slot ${slot}: Primary=[${prim.map(c => c.toFixed(2))}] Secondary=[${sec.map(c => c.toFixed(2))}]`);
                        }

                        // 1. Populate userData for uniforms
                        mat.userData.stackMap = { value: stackTex };
                        mat.userData.dyePrimary = { value: new THREE.Color(prim[0], prim[1], prim[2]) };
                        mat.userData.dyeSecondary = { value: new THREE.Color(sec[0], sec[1], sec[2]) };

                        const uDyePrimary = mat.userData.dyePrimary.value;
                        const uDyeSecondary = mat.userData.dyeSecondary.value;

                        // Ensure encoding is set to sRGB for base albedo
                        if (mat.map) {
                            mat.map.encoding = THREE.sRGBEncoding;
                        }

                        // Use neutral white tint for pure PBR lighting
                        mat.color.setRGB(1.0, 1.0, 1.0);

                        mat.needsUpdate = true;

                        mat.onBeforeCompile = (shader) => {
                            shader.uniforms.stackMap = mat.userData.stackMap;
                            shader.uniforms.dyePrimary = mat.userData.dyePrimary;
                            shader.uniforms.dyeSecondary = mat.userData.dyeSecondary;

                            // DEBUG: Guard against double-injection
                            if (shader.fragmentShader.includes('uniform sampler2D stackMap;')) {
                                // Already injected, skip to prevent 'redefinition' errors
                                return;
                            }

                            const uniformBlock = `
uniform sampler2D stackMap;
uniform vec3 dyePrimary;
uniform vec3 dyeSecondary;
`;

                            // --- ROBUST UNIFORM SETUP ---
                            // Ensure we have valid values for all uniforms to prevent crashes/compilation errors

                            // 1. Dyes - mat.userData contains {value: Color}, extract the Color
                            const uDyePrimary = (mat.userData.dyePrimary && mat.userData.dyePrimary.value) || new THREE.Color(1, 1, 1);
                            const uDyeSecondary = (mat.userData.dyeSecondary && mat.userData.dyeSecondary.value) || new THREE.Color(1, 1, 1);

                            // 2. Stack Map (Critical: Must be a Texture)
                            let uStackMap = stackTex;
                            if (!uStackMap) {
                                // Create a 1x1 dummy black texture if missing
                                // This prevents 'uniform sampler2D' from being unbound or invalid
                                if (!window._d2DummyStackTex) {
                                    const data = new Uint8Array([0, 0, 0, 255]); // Black, opaque
                                    window._d2DummyStackTex = new THREE.DataTexture(data, 1, 1, THREE.RGBAFormat);
                                    window._d2DummyStackTex.needsUpdate = true;
                                }
                                uStackMap = window._d2DummyStackTex;
                            }

                            shader.uniforms.stackMap = { value: uStackMap };
                            shader.uniforms.dyePrimary = { value: uDyePrimary };
                            shader.uniforms.dyeSecondary = { value: uDyeSecondary };

                            // --- SHADER INJECTION ---

                            // 1. Uniforms Declaration (Safe Injection)
                            // uniformBlock is already defined above at line 994


                            if (shader.fragmentShader.includes('#include <common>')) {
                                shader.fragmentShader = shader.fragmentShader.replace('#include <common>', '#include <common>\n' + uniformBlock);
                            } else {
                                shader.fragmentShader = uniformBlock + '\n' + shader.fragmentShader;
                            }

                            // 2. Logic Injection (Restored functionality)
                            // We replace <map_fragment> to intercept the diffuse color.
                            // Even if USE_MAP is false, we want to allow tinting (though stackMap sampling relies on vMapUv)

                            shader.fragmentShader = shader.fragmentShader.replace(
                                '#include <map_fragment>',
                                `
                                #include <map_fragment>
                                
                                // DYE MIXING LOGIC (REVAMPED v3)
                                // Dye REPLACES base albedo where mask is high
                                
                                #ifdef USE_MAP
                                    vec4 stackColor = texture2D(stackMap, vUv);
                                    vec3 baseColor = diffuseColor.rgb;
                                    
                                    // Detect if we have a valid mask. If not, we'll use a global fallback.
                                    float maskPresence = max(stackColor.r, stackColor.g);
                                    
                                    // Layer 1: Primary Dye (Red Mask)
                                    vec3 primaryDye = dyePrimary * 1.8; // Calibrated for ACESToneMapping
                                    vec3 color1 = mix(baseColor, primaryDye, stackColor.r);
                                    
                                    // Layer 2: Secondary Dye (Green Mask)
                                    vec3 secondaryDye = dyeSecondary * 1.8;
                                    vec3 finalColorResource = mix(color1, secondaryDye, stackColor.g);
                                    
                                    // ROBUST FALLBACK: If map is missing (black), force strong primary dye
                                    if (maskPresence < 0.05) {
                                        // Was 0.15 (mostly white), now 0.85 (mostly color)
                                        finalColorResource = mix(baseColor, primaryDye, 0.85);
                                    }
                                    
                                    diffuseColor.rgb = finalColorResource;
                                #else
                                    // Fallback: Use primary dye as solid color if map is missing
                                    diffuseColor.rgb = dyePrimary * 1.5;
                                #endif
                                `
                            );

                            // NOTE: Three.js r128 already reads correct ORM channels:
                            // - roughnessmap_fragment reads .g (green) ✓
                            // - metalnessmap_fragment reads .b (blue) ✓
                            // No manual override needed!

                            // Log for debugging
                            const texName = stackTex ? stackTex.name : (diffuseTex ? diffuseTex.name : 'No_Texture');
                            console.log(`[D2TGXLoader] Compiling Shader for [${texName}]`);




                        };
                    }

                    if (stackTex) {
                        console.log(`[D2TGXLoader] DEBUG: Visualizing STACK / MRC Texture: ${stackTex.name} `);
                    } else if (diffuseTex) {
                        console.log(`[D2TGXLoader] Material[${dyeIndex}]Slot:${slot} Pri:${isPrimary} Color: [${color.map(c => c.toFixed(2))}] Tex:${diffuseTex.name} `);
                    } else {
                        console.log(`[D2TGXLoader] Material[${dyeIndex}]Slot:${slot} Pri:${isPrimary} Color: [${color.map(c => c.toFixed(2))}] No Texture`);
                    }

                    // Attempt to flip normal map Y (DirectX -> OpenGL)
                    if (normalTex) normalTex.flipY = false;

                    const idx = materials.length;
                    materials.push(mat);
                    materialMap.set(matKey, idx);
                    return idx;
                };

                // 2. Process Parts and Assign Geometry Groups
                const validIndices = []; // Final indices array

                for (let p = 0; p < partLimit; p++) {
                    const part = stagePartList[p];
                    if (!part) continue;

                    // Skip low detail LODs (keep 0 and 1, maybe 2)
                    const lodValue = part.lod_category ? part.lod_category.value : 0;
                    if (lodValue > 1) continue; // Keep only high detail (0=Main, 1=Grip usually)

                    // Determine Material Index based on gear_dye_change_color_index
                    const dyeIndex = part.gear_dye_change_color_index || 0;

                    // Debug logs for first few parts to verify mapping
                    // Debug logs for first few parts to verify mapping
                    if (p < 5) {
                        // console.log(`[D2TGXLoader] Part ${ p } keys: `, Object.keys(part));
                        try {
                            console.log(`[D2TGXLoader] Part ${p} content(JSON): `, JSON.stringify(part));
                        } catch (err) {
                            console.log(`[D2TGXLoader] Part ${p} content: `, part);
                        }
                    }

                    const matIndex = getMaterialIndex(part);
                    const groupStart = validIndices.length;

                    // Extract and validate indices
                    const start = part.start_index || 0;
                    const count = part.index_count || 0;
                    const primitiveType = part.primitive_type || 3;
                    const numVerts = vertexBuffer.length;

                    if (primitiveType === 3) { // TRIANGLES
                        for (let i = 0; i < count; i += 3) {
                            const a = indexBuffer[start + i];
                            const b = indexBuffer[start + i + 1];
                            const c = indexBuffer[start + i + 2];

                            if (a < numVerts && b < numVerts && c < numVerts && a !== b && b !== c && a !== c) {
                                validIndices.push(a, b, c);
                            }
                        }
                    } else if (primitiveType === 5) { // STRIPS
                        for (let i = 0; i < count - 2; i++) {
                            const a = indexBuffer[start + i];
                            const b = indexBuffer[start + i + 1];
                            const c = indexBuffer[start + i + 2];
                            if (a < numVerts && b < numVerts && c < numVerts && a !== b && b !== c && a !== c) {
                                if (i & 1) validIndices.push(c, b, a);
                                else validIndices.push(a, b, c);
                            }
                        }
                    }

                    const groupCount = validIndices.length - groupStart;
                    if (groupCount > 0) {
                        geometry.addGroup(groupStart, groupCount, matIndex);
                    }
                }

                if (positions.length === 0 || validIndices.length === 0) {
                    console.warn('[D2TGXLoader] No valid geometry data generated');
                    continue;
                }

                geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
                geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
                geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
                // Duplicate UV to UV2 for Ambient Occlusion mapping (required by MeshStandardMaterial)
                geometry.setAttribute('uv2', new THREE.Float32BufferAttribute(uvs, 2));
                geometry.setIndex(validIndices);

                // DO NOT recompute normals, use the imported ones
                console.log('[D2TGXLoader] Using imported vertex normals');
                // geometry.computeVertexNormals();

                geometry.computeBoundingSphere();
                geometry.computeBoundingBox();

                console.log(`[D2TGXLoader] Created multi - material mesh: ${positions.length / 3} verts, ${validIndices.length / 3} tris`);

                // Create mesh with material array
                // Three.js handles rendering groups with corresponding materials automatically
                const mesh = new THREE.Mesh(geometry, materials);
                group.add(mesh);
            }

            return group.children.length > 0 ? group : null;
        }

        // Based on lowlidev's parseVertexBuffers
        parseVertexBuffers(tgxBin, renderMesh) {
            const vertexBuffer = [];
            const layouts = renderMesh.stage_part_vertex_stream_layout_definitions || [];

            if (layouts.length === 0) {
                console.warn('[D2TGXLoader] No vertex layout definitions');
                return vertexBuffer;
            }

            const formats = layouts[0].formats || [];

            // Log all vertex buffers
            console.log('[D2TGXLoader] Number of vertex buffers:', renderMesh.vertex_buffers.length);
            for (let i = 0; i < renderMesh.vertex_buffers.length; i++) {
                const vb = renderMesh.vertex_buffers[i];
                console.log(`  Buffer ${i}: ${vb.file_name}, stride = ${vb.stride_byte_size} `);
            }

            for (let vbIndex = 0; vbIndex < renderMesh.vertex_buffers.length; vbIndex++) {
                const vbInfo = renderMesh.vertex_buffers[vbIndex];
                const vbFile = tgxBin.files[vbInfo.file_name];

                if (!vbFile) {
                    console.warn('[D2TGXLoader] Missing vertex file:', vbInfo.file_name);
                    continue;
                }

                const vbData = vbFile.data;
                const format = formats[vbIndex];

                if (!format || !format.elements) {
                    console.warn('[D2TGXLoader] No format for vertex buffer', vbIndex);
                    continue;
                }

                const stride = vbInfo.stride_byte_size || 32;
                let vertexIndex = 0;

                // Log vertex format details for ALL buffers
                console.log(`[D2TGXLoader] Buffer ${vbIndex} format elements: `);
                for (const el of format.elements) {
                    console.log(`  - ${el.semantic}: type = ${el.type}, normalized = ${el.normalized} `);
                }

                for (let v = 0; v < vbInfo.byte_size; v += stride) {
                    if (vertexBuffer.length <= vertexIndex) {
                        vertexBuffer[vertexIndex] = {};
                    }

                    let vertexOffset = v;

                    // Log raw bytes for first 4 vertices of Buffer 1 (UVs) to debug format
                    if (vbIndex === 1 && vertexIndex < 8) {
                        const hex = [];
                        for (let k = 0; k < stride; k++) {
                            if (v + k < vbData.length) {
                                hex.push(vbData[v + k].toString(16).padStart(2, '0').toUpperCase());
                            }
                        }
                        console.log(`[D2TGXLoader] Buffer 1 Vertex ${vertexIndex} Raw Hex: ${hex.join(' ')} `);
                    }

                    for (const element of format.elements) {
                        const values = [];
                        const elementType = (element.type || '').replace('_vertex_format_attribute_', '');
                        const semantic = (element.semantic || '').replace('_tfx_vb_semantic_', '');

                        // Parse based on type
                        if (elementType.startsWith('float')) {
                            const count = parseInt(elementType.replace('float', '')) || 4;
                            for (let j = 0; j < count; j++) {
                                values.push(Utils.float(vbData, vertexOffset));
                                vertexOffset += 4;
                            }
                        } else if (elementType.startsWith('short')) {
                            const count = parseInt(elementType.replace('short', '').replace('n', '')) || 4;
                            for (let j = 0; j < count; j++) {
                                let val;
                                // Treat as SHORT (Signed)
                                val = Utils.short(vbData, vertexOffset);
                                if (element.normalized) val = Utils.normalize(val, 16);

                                values.push(val);
                                vertexOffset += 2;
                            }
                        } else if (elementType.startsWith('ubyte')) {
                            const count = parseInt(elementType.replace('ubyte', '').replace('n', '')) || 4;
                            for (let j = 0; j < count; j++) {
                                let val = Utils.ubyte(vbData, vertexOffset);
                                if (element.normalized) val = Utils.unormalize(val, 8);
                                values.push(val);
                                vertexOffset += 1;
                            }
                        } else if (elementType.startsWith('byte')) {
                            const count = parseInt(elementType.replace('byte', '').replace('n', '')) || 4;
                            for (let j = 0; j < count; j++) {
                                let val = Utils.byte(vbData, vertexOffset);
                                if (element.normalized) val = Utils.normalize(val, 8);
                                values.push(val);
                                vertexOffset += 1;
                            }
                        } else if (elementType.startsWith('ushort')) {
                            const count = parseInt(elementType.replace('ushort', '').replace('n', '')) || 4;
                            for (let j = 0; j < count; j++) {
                                let val = Utils.ushort(vbData, vertexOffset);
                                if (element.normalized) val = Utils.unormalize(val, 16);
                                values.push(val);
                                vertexOffset += 2;
                            }
                        } else if (elementType.startsWith('half') || elementType.includes('float16')) {
                            // Half-Float (Float16) - CRITICAL for UVs per technical report
                            const count = parseInt(elementType.replace('half', '').replace('float16', '').replace('n', '')) || 2;
                            for (let j = 0; j < count; j++) {
                                values.push(Utils.float16(vbData, vertexOffset));
                                vertexOffset += 2;
                            }
                        } else {
                            // Default: skip 4 bytes per unknown element
                            console.warn('[D2TGXLoader] Unknown element type:', elementType);
                            vertexOffset += 4;
                            continue;
                        }

                        // Store by semantic
                        const semanticIndex = element.semantic_index || 0;
                        vertexBuffer[vertexIndex][semantic + semanticIndex] = values;
                    }

                    vertexIndex++;
                }
            }

            return vertexBuffer;
        }
    }

    window.D2TGXLoader = D2TGXLoader;
    THREE.D2TGXLoader = D2TGXLoader;

    console.log('[D2TGXLoader] Loaded v2');

})();
