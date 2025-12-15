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
        string: (data, offset, length) => {
            let str = '';
            for (let i = 0; i < length; i++) {
                const chr = data[offset + i];
                if (chr === 0) break;
                str += String.fromCharCode(chr);
            }
            return str;
        },
        // Normalize signed value to -1 to 1
        normalize: (value, bits) => {
            const max = Math.pow(2, bits - 1) - 1;
            return Math.max(value / max, -1);
        },
        // Normalize unsigned value to 0 to 1
        unormalize: (value, bits) => {
            const max = Math.pow(2, bits) - 1;
            return value / max;
        }
    };

    class D2TGXLoader {
        constructor() {
            this.proxyUrl = PROXY_URL;
        }

        async load(config) {
            const {
                itemHashes = [],
                isFemale = false,
                classType = 1,
                onProgress = () => { },
                onError = console.error
            } = config;

            console.log('[D2TGXLoader] Loading items:', itemHashes);
            onProgress({ message: 'Cargando datos del manifest...' });

            try {
                const meshes = [];

                for (const itemHash of itemHashes) {
                    onProgress({ message: `Cargando item ${itemHash}...` });

                    const gearAsset = await this.getGearAsset(itemHash);
                    if (!gearAsset) {
                        console.warn('[D2TGXLoader] No gear asset for', itemHash);
                        continue;
                    }

                    const mesh = await this.loadItemGeometry(gearAsset, isFemale, onProgress);
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

        async loadItemGeometry(gearAsset, isFemale, onProgress) {
            const content = gearAsset.content;
            if (!content || content.length === 0) return null;

            const contentEntry = content[0];
            const geometryFiles = contentEntry.geometry || [];
            if (geometryFiles.length === 0) return null;

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
                    const mesh = await this.loadTGXM(geometryFile, onProgress);
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

        async loadTGXM(filename, onProgress) {
            let cleanName = filename;
            if (cleanName.endsWith('.bin')) cleanName = cleanName.slice(0, -4);

            const url = `${this.proxyUrl}/api/geometry/platform/mobile/geometry/${cleanName}`;
            console.log('[D2TGXLoader] Loading TGXM:', url);

            try {
                const response = await fetch(url);
                if (!response.ok) throw new Error(`HTTP ${response.status}`);

                const buffer = await response.arrayBuffer();
                const data = new Uint8Array(buffer);

                return this.parseTGXM(data, cleanName);

            } catch (error) {
                console.error('[D2TGXLoader] Failed to load TGXM:', error);
                return null;
            }
        }

        parseTGXM(data, filename) {
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
            return this.parseTGXAsset({ metadata, files, lookup }, filename);
        }

        // Based on lowlidev's parseTGXAsset
        parseTGXAsset(tgxBin, geometryHash) {
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

                // Get position transform
                const positionOffset = renderMesh.position_offset || [0, 0, 0];
                const positionScale = renderMesh.position_scale || [1, 1, 1];
                const texcoordOffset = renderMesh.texcoord_offset || [0, 0];
                const texcoordScale = renderMesh.texcoord_scale || [1, 1];

                // Check if positions are normalized (values between -1 and 1)
                let needsScale = false;
                if (vertexBuffer.length > 0 && vertexBuffer[0].position0) {
                    const samplePos = vertexBuffer[0].position0;
                    const maxVal = Math.max(Math.abs(samplePos[0]), Math.abs(samplePos[1]), Math.abs(samplePos[2]));
                    needsScale = maxVal <= 1.5; // If all values are small, they're normalized
                    console.log('[D2TGXLoader] Sample position:', samplePos, 'needsScale:', needsScale);
                }

                console.log('[D2TGXLoader] VertexBuffer:', vertexBuffer.length, 'vertices, scale:', positionScale, 'needsScale:', needsScale);

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
                    const pos = vertex.position0 || [0, 0, 0];
                    const norm = vertex.normal0 || [0, 1, 0];
                    const uv = vertex.texcoord0 || [0, 0];

                    let x = pos[0], y = pos[1], z = pos[2];

                    // Apply scale if positions are normalized
                    if (needsScale) {
                        x = x * positionScale[0] + positionOffset[0];
                        y = y * positionScale[1] + positionOffset[1];
                        z = z * positionScale[2] + positionOffset[2];
                    }

                    positions.push(x, y, z);
                    normals.push(-norm[0], -norm[1], -norm[2]);

                    // Apply texcoord transform for UVs
                    uvs.push(uv[0] * texcoordScale[0] + texcoordOffset[0],
                        uv[1] * texcoordScale[1] + texcoordOffset[1]);
                }

                // Process parts
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
                        }
                    }
                }

                if (positions.length === 0 || indices.length === 0) {
                    console.warn('[D2TGXLoader] No geometry data');
                    continue;
                }

                geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
                geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
                geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
                geometry.setIndex(indices);
                geometry.computeBoundingSphere();

                console.log('[D2TGXLoader] Created mesh:', positions.length / 3, 'verts,', indices.length / 3, 'tris');

                const material = new THREE.MeshStandardMaterial({
                    color: 0x888888,
                    metalness: 0.6,
                    roughness: 0.4,
                    side: THREE.DoubleSide
                });

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
                        } else {
                            // Default: skip 4 bytes per unknown element
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
