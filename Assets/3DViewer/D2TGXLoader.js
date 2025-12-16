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

        // Extract gear dye colors from content entry
        extractGearDyes(contentEntry) {
            // NOTE: Destiny 2 gear asset doesn't include dye colors directly
            // Colors are in texture files or manifest - keeping structure for future use

            const dyes = {
                default: [],
                custom: [],
                locked: []
            };

            // Parse default dyes
            if (contentEntry.default_dyes) {
                for (const dye of contentEntry.default_dyes) {
                    dyes.default.push(this.parseDyeData(dye));
                }
            }

            // Parse custom dyes (shader colors)
            if (contentEntry.custom_dyes) {
                for (const dye of contentEntry.custom_dyes) {
                    dyes.custom.push(this.parseDyeData(dye));
                }
            }

            // Parse locked dyes (exotic items)
            if (contentEntry.locked_dyes) {
                for (const dye of contentEntry.locked_dyes) {
                    dyes.locked.push(this.parseDyeData(dye));
                }
            }

            console.log('[D2TGXLoader] Extracted dyes:', dyes);
            return dyes;
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
            return {
                slotTypeIndex: dye.slot_type_index || 0,
                primaryColor: props.primary_albedo_tint || [0.5, 0.5, 0.5],
                secondaryColor: props.secondary_albedo_tint || [0.3, 0.3, 0.3],
                wornColor: props.worn_albedo_tint || [0.2, 0.2, 0.2],
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
                    result.diffuse.push(allTextures[idx]);
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
                        console.log('[D2TGXLoader] Loaded texture image:', name, isPng ? 'PNG' : 'JPEG');
                    }
                }

                // Cache and return first texture (usually diffuse)
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
                isFemale = false,
                classType = 1,
                onProgress = () => { },
                onError = console.error
            } = config;

            console.log('[D2TGXLoader] Loading items:', itemHashes);
            console.log('[D2TGXLoader] With shaders:', shaderHashes);
            onProgress({ message: 'Cargando datos del manifest...' });

            try {
                const meshes = [];

                for (let i = 0; i < itemHashes.length; i++) {
                    const itemHash = itemHashes[i];
                    const shaderHash = shaderHashes[i] || 0;

                    onProgress({ message: `Cargando item ${itemHash}...` });

                    const gearAsset = await this.getGearAsset(itemHash);
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

        // Load shader data from local proxy (queries Bungie API)
        async loadShaderFromLowlidev(shaderHash) {
            try {
                // Use our local proxy which queries Bungie's manifest API
                const url = `${this.proxyUrl}/api/shader/${shaderHash}`;
                console.log('[D2TGXLoader] Trying proxy for shader:', shaderHash);

                const response = await fetch(url);
                if (!response.ok) {
                    console.warn('[D2TGXLoader] Shader API failed:', response.status);
                    return null;
                }

                const data = await response.json();
                console.log('[D2TGXLoader] Shader API response:', data);

                // Bungie API returns { ErrorCode, Response: { ... } }
                if (data.ErrorCode === 1 && data.Response) {
                    const gearAsset = data.Response;
                    console.log('[D2TGXLoader] Shader gearAsset:', Object.keys(gearAsset));

                    // If shader has gear JSON, load it
                    if (gearAsset.gear && gearAsset.gear.length > 0) {
                        const gearPath = gearAsset.gear[0];
                        const gearUrl = `${this.proxyUrl}/api/gear/${gearPath}`;
                        console.log('[D2TGXLoader] Loading shader gear from:', gearUrl);

                        const gearResponse = await fetch(gearUrl);
                        if (gearResponse.ok) {
                            const gearData = await gearResponse.json();
                            console.log('[D2TGXLoader] Shader gear data keys:', Object.keys(gearData));

                            // Extract dye colors from shader gear
                            const dyeColors = this.extractDyeColors(gearData);
                            if (dyeColors && Object.keys(dyeColors).length > 0) {
                                console.log('[D2TGXLoader] Got shader dyes!', dyeColors);
                                return dyeColors;
                            }
                        }
                    }
                }

                return null;
            } catch (error) {
                console.warn('[D2TGXLoader] Error loading shader:', error);
                return null;
            }
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
            let loadedTexture = null;
            if (texturePaths.diffuse && texturePaths.diffuse.length > 0) {
                const textureFile = texturePaths.diffuse[0];
                console.log('[D2TGXLoader] Attempting to load texture:', textureFile);
                onProgress({ message: 'Cargando texturas...' });
                try {
                    loadedTexture = await this.loadTextureFromTGXM(textureFile);
                } catch (e) {
                    console.warn('[D2TGXLoader] Texture load failed:', e);
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
                    const mesh = await this.loadTGXM(geometryFile, onProgress, resolvedDyeColors, texturePaths, loadedTexture);
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

        async loadTGXM(filename, onProgress, gearDyes = {}, texturePaths = {}, loadedTexture = null) {
            let cleanName = filename;
            if (cleanName.endsWith('.bin')) cleanName = cleanName.slice(0, -4);

            const url = `${this.proxyUrl}/api/geometry/platform/mobile/geometry/${cleanName}`;
            console.log('[D2TGXLoader] Loading TGXM:', url);

            try {
                const response = await fetch(url);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);

                const buffer = await response.arrayBuffer();
                const data = new Uint8Array(buffer);

                return this.parseTGXM(data, cleanName, gearDyes, texturePaths, loadedTexture);

            } catch (error) {
                console.error('[D2TGXLoader] Failed to load TGXM:', error);
                return null;
            }
        }

        parseTGXM(data, filename, gearDyes = {}, texturePaths = {}, loadedTexture = null) {
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
            return this.parseTGXAsset({ metadata, files, lookup }, filename, gearDyes, texturePaths, loadedTexture);
        }

        // Based on lowlidev's parseTGXAsset
        parseTGXAsset(tgxBin, geometryHash, gearDyes = {}, texturePaths = {}, loadedTexture = null) {
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

                // Position format is float4 (not normalized) - no scaling needed
                // Scale would only be needed for short4n normalized positions
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
                    uvs.push(uv[0] * texcoordScale[0] + texcoordOffset[0],
                        uv[1] * texcoordScale[1] + texcoordOffset[1]);
                }

                // Process parts
                let triangleCount = 0;
                for (let p = 0; p < partLimit; p++) {
                    const part = stagePartList[p];
                    if (!part) continue;

                    // Check LOD category (0-3 are high detail)
                    const lodValue = part.lod_category ? part.lod_category.value : 0;
                    if (lodValue > 3) continue;

                    const start = part.start_index || 0;
                    const count = part.index_count || 0;
                    const primitiveType = part.primitive_type || 3;

                    if (primitiveType === 3) {
                        // TRIANGLES
                        for (let i = 0; i < count; i++) {
                            indices.push(indexBuffer[start + i]);
                        }
                        triangleCount += count / 3;
                    } else if (primitiveType === 5) {
                        // TRIANGLE_STRIP - convert to triangles
                        for (let i = 0; i < count - 2; i++) {
                            const a = indexBuffer[start + i];
                            const b = indexBuffer[start + i + 1];
                            const c = indexBuffer[start + i + 2];
                            if (i & 1) {
                                indices.push(c, b, a);
                            } else {
                                indices.push(a, b, c);
                            }
                            triangleCount++;
                        }
                    }
                }

                // Log sample triangle for debugging
                if (indices.length >= 3) {
                    const numVerts = vertexBuffer.length;
                    let validTris = 0;
                    let invalidTris = 0;
                    for (let i = 0; i < indices.length; i += 3) {
                        const a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        if (a < numVerts && b < numVerts && c < numVerts) {
                            validTris++;
                        } else {
                            invalidTris++;
                        }
                    }
                    console.log(`[D2TGXLoader] Triangles: ${validTris} valid, ${invalidTris} invalid (verts: ${numVerts})`);

                    // Log first triangle positions
                    const i0 = indices[0], i1 = indices[1], i2 = indices[2];
                    if (i0 < numVerts && i1 < numVerts && i2 < numVerts) {
                        const v0 = vertexBuffer[i0].position0;
                        const v1 = vertexBuffer[i1].position0;
                        const v2 = vertexBuffer[i2].position0;
                        console.log(`[D2TGXLoader] First tri vertices:`,
                            `[${v0[0]?.toFixed(2)}, ${v0[1]?.toFixed(2)}, ${v0[2]?.toFixed(2)}]`,
                            `[${v1[0]?.toFixed(2)}, ${v1[1]?.toFixed(2)}, ${v1[2]?.toFixed(2)}]`,
                            `[${v2[0]?.toFixed(2)}, ${v2[1]?.toFixed(2)}, ${v2[2]?.toFixed(2)}]`);
                    }
                }

                if (positions.length === 0 || indices.length === 0) {
                    console.warn('[D2TGXLoader] No geometry data');
                    continue;
                }

                // Filter out invalid triangles (indices pointing to non-existent vertices)
                const numVerts = vertexBuffer.length;
                const validIndices = [];
                let skippedTris = 0;

                for (let i = 0; i < indices.length; i += 3) {
                    const a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    // Check if all indices are valid
                    if (a < numVerts && b < numVerts && c < numVerts &&
                        a !== undefined && b !== undefined && c !== undefined) {
                        // Skip degenerate triangles (two or more identical indices)
                        if (a !== b && b !== c && a !== c) {
                            validIndices.push(a, b, c);
                        } else {
                            skippedTris++;
                        }
                    } else {
                        skippedTris++;
                    }
                }

                if (skippedTris > 0) {
                    console.log(`[D2TGXLoader] Skipped ${skippedTris} invalid/degenerate triangles`);
                }

                geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
                geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
                geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
                geometry.setIndex(validIndices);

                // Compute proper normals for better lighting
                geometry.computeVertexNormals();
                geometry.computeBoundingSphere();
                geometry.computeBoundingBox();

                // Log bounding box to verify 3D depth
                const box = geometry.boundingBox;
                console.log('[D2TGXLoader] BoundingBox:',
                    'X:', box.min.x.toFixed(2), 'to', box.max.x.toFixed(2),
                    'Y:', box.min.y.toFixed(2), 'to', box.max.y.toFixed(2),
                    'Z:', box.min.z.toFixed(2), 'to', box.max.z.toFixed(2));

                console.log('[D2TGXLoader] Created mesh:', positions.length / 3, 'verts,', indices.length / 3, 'tris');

                // Create material with gearstack texture and dye colors
                const materialParams = {
                    metalness: 0.75,
                    roughness: 0.35,
                    side: THREE.DoubleSide,
                    flatShading: false,
                    envMapIntensity: 1.0
                };

                // Get dye color for this mesh (slot 0 is typically the primary armor color)
                let dyeColor = null;
                if (gearDyes && Object.keys(gearDyes).length > 0) {
                    // Use first available dye slot
                    const firstSlot = Object.keys(gearDyes)[0];
                    const dye = gearDyes[firstSlot];
                    if (dye && dye.primaryColor) {
                        const pc = dye.primaryColor;
                        // Log actual RGB values
                        console.log(`[D2TGXLoader] Dye RGB: R=${pc[0].toFixed(3)} G=${pc[1].toFixed(3)} B=${pc[2].toFixed(3)}`);

                        // Create color - values are typically 0-1 range
                        dyeColor = new THREE.Color(pc[0], pc[1], pc[2]);

                        // If color is too dark/gray, boost saturation
                        const luminance = 0.299 * pc[0] + 0.587 * pc[1] + 0.114 * pc[2];
                        console.log(`[D2TGXLoader] Luminance: ${luminance.toFixed(3)}`);
                    }
                }

                if (loadedTexture) {
                    console.log('[D2TGXLoader] Applying gearstack texture with dye color');
                    materialParams.map = loadedTexture;

                    // Apply dye color as tint, or white if no dye available
                    if (dyeColor) {
                        materialParams.color = dyeColor;
                    } else {
                        materialParams.color = 0xffffff;
                    }
                    materialParams.metalness = 0.7;
                    materialParams.roughness = 0.4;
                } else {
                    // No texture - use dye color directly or fallback
                    if (dyeColor) {
                        materialParams.color = dyeColor;
                    } else {
                        materialParams.color = 0xb8b8c8; // Fallback silver
                    }
                    materialParams.metalness = 0.85;
                }

                const material = new THREE.MeshStandardMaterial(materialParams);

                const mesh = new THREE.Mesh(geometry, material);
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
                console.log(`  Buffer ${i}: ${vb.file_name}, stride=${vb.stride_byte_size}`);
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
                console.log(`[D2TGXLoader] Buffer ${vbIndex} format elements:`);
                for (const el of format.elements) {
                    console.log(`  - ${el.semantic}: type=${el.type}, normalized=${el.normalized}`);
                }

                for (let v = 0; v < vbInfo.byte_size; v += stride) {
                    if (vertexBuffer.length <= vertexIndex) {
                        vertexBuffer[vertexIndex] = {};
                    }

                    let vertexOffset = v;

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
                                let val = Utils.short(vbData, vertexOffset);
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
