/**
 * Guardian 3D Viewer - D2 Compatible
 * Uses D2TGXLoader for real Destiny 2 models via local proxy
 */

let camera, scene, renderer, controls;
let guardian = null;
let clock = new THREE.Clock();
let isLoading = false;

// Model rotation control (turntable)
let isDragging = false;
let previousMouseX = 0;
let modelRotationY = 0;

function init() {
    const container = document.getElementById('container');

    scene = new THREE.Scene();

    // Camera with wider FOV (60 instead of 30) for better 3D perception
    camera = new THREE.PerspectiveCamera(60, container.clientWidth / container.clientHeight, 0.1, 1000);
    camera.position.set(0, 2, 5);  // Closer position
    scene.add(camera);

    renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        powerPreference: "high-performance"
    });
    renderer.setClearColor(0x000000, 0);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.shadowMap.enabled = true;
    renderer.outputEncoding = THREE.sRGBEncoding;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;
    renderer.physicallyCorrectLights = true;
    container.appendChild(renderer.domElement);

    setupLighting();

    controls = new THREE.OrbitControls(camera, renderer.domElement);
    controls.rotateSpeed = 1.2;
    controls.zoomSpeed = 0.6;
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.enablePan = false;
    controls.enableRotate = true;  // Enable rotation
    controls.enableZoom = true;
    controls.minDistance = 1;
    controls.maxDistance = 50;
    controls.target.set(0, 0, 0);  // Center target at origin

    window.addEventListener('resize', onWindowResize, false);
    animate();

    console.log('[Guardian3D] Viewer initialized');
}

function setupLighting() {
    // Ambient - soft white for base visibility
    scene.add(new THREE.AmbientLight(0xffffff, 0.4));

    // Key Light - neutral white from front-right
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.0);
    keyLight.position.set(3, 5, 4);
    keyLight.castShadow = true;
    scene.add(keyLight);

    // Fill Light - softer white from left
    const fillLight = new THREE.DirectionalLight(0xf0f0f0, 0.5);
    fillLight.position.set(-3, 3, 3);
    scene.add(fillLight);

    // Rim Light - white backlight for silhouette
    const rimLight = new THREE.DirectionalLight(0xffffff, 0.6);
    rimLight.position.set(0, 2, -5);
    scene.add(rimLight);

    // Top light - soft from above
    const topLight = new THREE.DirectionalLight(0xffffff, 0.4);
    topLight.position.set(0, 10, 0);
    scene.add(topLight);

    // Subtle cool accent for depth
    const accentLight = new THREE.PointLight(0xaaccff, 0.3, 10);
    accentLight.position.set(-1, 0, 2);
    scene.add(accentLight);
}

function onWindowResize() {
    const container = document.getElementById('container');
    camera.aspect = container.clientWidth / container.clientHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(container.clientWidth, container.clientHeight);
}

function animate() {
    requestAnimationFrame(animate);

    const elapsed = clock.getElapsedTime();

    if (guardian) {
        // Natural breathing animation
        const breathCycle = Math.sin(elapsed * 1.2) * 0.003;  // Slower, more natural
        const breathCycle2 = Math.sin(elapsed * 0.8) * 0.001;  // Secondary subtle

        // Chest expansion (Y scale)
        guardian.scale.y = 1 + breathCycle;

        // Slight shoulder movement (X scale)
        guardian.scale.x = 1 + breathCycle2 * 0.5;

        // Very subtle forward/back sway
        guardian.rotation.x = Math.sin(elapsed * 0.5) * 0.005;
    }

    controls.update();
    renderer.render(scene, camera);
}

/**
 * Load FBX model exported from Charm Tool
 * This loads REAL 3D models from local files
 */
async function loadFBXModel(modelPath) {
    if (!window.THREE || !window.THREE.FBXLoader) {
        console.warn('[Guardian3D] FBXLoader not available');
        return false;
    }

    try {
        updateProgress('Cargando modelo FBX real...');
        const loader = new THREE.FBXLoader();

        const fbx = await new Promise((resolve, reject) => {
            loader.load(
                modelPath,
                (result) => resolve(result),
                (progress) => {
                    if (progress.total > 0) {
                        const pct = Math.round(progress.loaded / progress.total * 100);
                        updateProgress(`Cargando FBX: ${pct}%`);
                    }
                },
                (error) => reject(error)
            );
        });

        if (fbx) {
            // Debug: analyze FBX structure
            let meshCount = 0;
            let totalVertices = 0;
            fbx.traverse((child) => {
                if (child.isMesh) {
                    meshCount++;
                    const geom = child.geometry;
                    const posAttr = geom.getAttribute('position');
                    if (posAttr) {
                        totalVertices += posAttr.count;
                        // Log first mesh bounds
                        if (meshCount === 1) {
                            geom.computeBoundingBox();
                            const bb = geom.boundingBox;
                            console.log('[Guardian3D] First mesh bounding box:',
                                'X:', bb.min.x.toFixed(2), 'to', bb.max.x.toFixed(2),
                                'Y:', bb.min.y.toFixed(2), 'to', bb.max.y.toFixed(2),
                                'Z:', bb.min.z.toFixed(2), 'to', bb.max.z.toFixed(2));

                            // Check actual vertex positions
                            let minY = Infinity, maxY = -Infinity;
                            for (let i = 0; i < posAttr.count && i < 100; i++) {
                                const y = posAttr.getY(i);
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                            console.log('[Guardian3D] Y-range (first 100 verts):', minY.toFixed(4), 'to', maxY.toFixed(4));
                        }
                    }
                }
            });
            console.log('[Guardian3D] FBX loaded:', meshCount, 'meshes,', totalVertices, 'total vertices');

            // Apply materials if needed
            fbx.traverse((child) => {
                if (child.isMesh) {
                    if (!child.material || child.material.name === '') {
                        child.material = new THREE.MeshStandardMaterial({
                            color: 0xaaaaaa,
                            metalness: 0.6,
                            roughness: 0.4,
                            side: THREE.DoubleSide
                        });
                    }
                }
            });

            // Log bounding box for debugging
            const boxBefore = new THREE.Box3().setFromObject(fbx);
            const sizeBefore = boxBefore.getSize(new THREE.Vector3());
            console.log('[Guardian3D] FBX BoundingBox BEFORE rotation:',
                'X:', sizeBefore.x.toFixed(2),
                'Y:', sizeBefore.y.toFixed(2),
                'Z:', sizeBefore.z.toFixed(2));

            // Rotate model to stand upright (Z-up to Y-up conversion)
            fbx.rotation.x = -Math.PI / 2;

            // Center and scale
            fbx.updateMatrixWorld(true);
            const box = new THREE.Box3().setFromObject(fbx);
            const center = box.getCenter(new THREE.Vector3());
            const size = box.getSize(new THREE.Vector3());

            console.log('[Guardian3D] FBX BoundingBox:',
                'X:', size.x.toFixed(2),
                'Y:', size.y.toFixed(2),
                'Z:', size.z.toFixed(2));

            // Center the model at origin
            fbx.position.set(-center.x, -center.y, -center.z);

            // Add directly to scene
            guardian = fbx;
            scene.add(guardian);

            // Position camera at eye level, looking at center of model
            const modelHeight = size.y;
            const cameraDistance = Math.max(size.x, size.z) * 3;
            camera.position.set(0, modelHeight * 0.5, cameraDistance);
            controls.target.set(0, 0, 0);

            // Limit vertical rotation for turntable-style viewing
            controls.minPolarAngle = Math.PI * 0.3;  // Limit looking from above
            controls.maxPolarAngle = Math.PI * 0.7;  // Limit looking from below
            controls.update();

            console.log('[Guardian3D] FBX model loaded successfully!');
            return true;
        }
    } catch (error) {
        console.warn('[Guardian3D] Failed to load FBX:', error.message);
    }
    return false;
}

/**
 * Load Collada models from the proxy endpoint
 * Uses Destiny-Collada-Generator to create real 3D models
 */
async function loadColladaModels(itemHashes) {
    if (!window.THREE || !window.THREE.ColladaLoader) {
        console.warn('[Guardian3D] ColladaLoader not available');
        return false;
    }

    const loader = new THREE.ColladaLoader();
    const group = new THREE.Group();
    let loadedCount = 0;

    for (const hash of itemHashes) {
        try {
            updateProgress(`Generando modelo ${loadedCount + 1}/${itemHashes.length}...`);
            const url = `http://localhost:5050/api/collada/${hash}`;

            const collada = await new Promise((resolve, reject) => {
                loader.load(
                    url,
                    (result) => resolve(result),
                    (progress) => {
                        if (progress.total > 0) {
                            const pct = Math.round(progress.loaded / progress.total * 100);
                            updateProgress(`Cargando ${hash}: ${pct}%`);
                        }
                    },
                    (error) => reject(error)
                );
            });

            if (collada && collada.scene) {
                // Apply materials
                collada.scene.traverse((child) => {
                    if (child.isMesh) {
                        child.material = new THREE.MeshStandardMaterial({
                            color: 0xaaaaaa,
                            metalness: 0.7,
                            roughness: 0.3,
                            side: THREE.DoubleSide
                        });
                    }
                });
                group.add(collada.scene);
                loadedCount++;
                console.log(`[Guardian3D] Collada loaded: ${hash}`);
            }
        } catch (error) {
            console.warn(`[Guardian3D] Failed to load Collada ${hash}:`, error.message);
        }
    }

    if (loadedCount === 0) {
        return false;
    }

    // Center and scale the model
    const box = new THREE.Box3().setFromObject(group);
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());

    group.position.set(-center.x, -center.y, -center.z);

    const wrapper = new THREE.Group();
    wrapper.add(group);

    // Scale to fit view
    const maxDim = Math.max(size.x, size.y, size.z);
    const scale = 200 / (maxDim || 1);
    wrapper.scale.set(scale, scale, scale);

    guardian = wrapper;
    scene.add(guardian);

    console.log(`[Guardian3D] Loaded ${loadedCount}/${itemHashes.length} Collada models`);
    return true;
}

/**
 * Load guardian using D2TGXLoader (fallback)
 */
async function loadGuardian(config) {
    if (isLoading) return;
    isLoading = true;

    showLoading(true);
    hideError();
    updateProgress('Conectando con servidor...');

    if (guardian) {
        scene.remove(guardian);
        guardian = null;
    }

    var itemHashes = config.itemHashes || [];
    var shaderHashes = config.shaderHashes || [];
    var classType = config.classType || 1;
    var isFemale = config.isFemale || false;

    if (itemHashes.length === 0) {
        console.warn('[Guardian3D] No item hashes, showing placeholder');
        showPlaceholder(classType);
        return;
    }

    console.log('[Guardian3D] Loading guardian...');
    console.log('[Guardian3D] Items:', itemHashes);
    console.log('[Guardian3D] Shaders:', shaderHashes, 'Non-zero:', shaderHashes.filter(s => s > 0).length);

    // First try D2TGXLoader - this loads the ACTUAL guardian items from Bungie API
    updateProgress('Cargando modelo del guardián...');
    console.log('[Guardian3D] Trying D2TGXLoader (real guardian model)...');

    // Use our D2TGXLoader
    if (window.D2TGXLoader) {
        try {
            const loader = new D2TGXLoader();

            const mesh = await loader.load({
                itemHashes: itemHashes,
                shaderHashes: shaderHashes,
                isFemale: isFemale,
                classType: classType,
                onProgress: (event) => {
                    if (event && event.message) {
                        updateProgress(event.message);
                    }
                },
                onError: (error) => {
                    console.error('[Guardian3D] D2TGXLoader error:', error);
                }
            });

            if (mesh && mesh.children && mesh.children.length > 0) {
                // Compute bounding box of entire group
                const box = new THREE.Box3().setFromObject(mesh);
                const center = box.getCenter(new THREE.Vector3());
                const size = box.getSize(new THREE.Vector3());

                console.log('[Guardian3D] TGX Model center BEFORE:', center);
                console.log('[Guardian3D] TGX Model size:', size);

                // First rotate to correct orientation (Z-up to Y-up)
                mesh.rotation.x = -Math.PI / 2;
                mesh.updateMatrixWorld(true);

                // Now recalculate bounds AFTER rotation
                const boxAfter = new THREE.Box3().setFromObject(mesh);
                const centerAfter = boxAfter.getCenter(new THREE.Vector3());
                const sizeAfter = boxAfter.getSize(new THREE.Vector3());

                console.log('[Guardian3D] TGX Model center AFTER:', centerAfter);

                // NOW center the model at origin using the rotated center
                mesh.position.set(
                    mesh.position.x - centerAfter.x,
                    mesh.position.y - centerAfter.y,
                    mesh.position.z - centerAfter.z
                );

                // Create wrapper for turntable rotation
                const wrapper = new THREE.Group();
                wrapper.add(mesh);

                // Rotate wrapper to face camera
                wrapper.rotation.y = -Math.PI / 2;  // -90 degrees to face camera

                guardian = wrapper;
                scene.add(guardian);

                // Position camera to see full model
                const modelHeight = sizeAfter.y;
                const cameraDistance = Math.max(sizeAfter.x, sizeAfter.y, sizeAfter.z) * 2.5;

                // Camera and target at origin (model is now centered at 0,0,0)
                camera.position.set(0, 0, cameraDistance);
                controls.target.set(0, 0, 0);  // Model is at origin

                // Lock vertical angle for perfect horizontal turntable rotation
                controls.minPolarAngle = Math.PI * 0.5;
                controls.maxPolarAngle = Math.PI * 0.5;
                controls.minDistance = cameraDistance * 0.5;
                controls.maxDistance = cameraDistance * 2;
                controls.update();

                console.log('[Guardian3D] Real D2 model loaded!');
                showLoading(false);
                isLoading = false;
                return;
            }

        } catch (error) {
            console.error('[Guardian3D] D2TGXLoader failed:', error);
        }
    }

    // Fallback to placeholder
    console.log('[Guardian3D] Falling back to placeholder');
    showPlaceholder(classType);
}

/**
 * Stylized placeholder model
 */
function showPlaceholder(classType) {
    if (guardian) {
        scene.remove(guardian);
    }

    var classColors = {
        0: { primary: 0x2980B9, secondary: 0x1a5276, glow: 0x3498DB },  // Titan
        1: { primary: 0x8E44AD, secondary: 0x5b2c6f, glow: 0x9D4EDD },  // Hunter
        2: { primary: 0xD4AC0D, secondary: 0x9a7d0a, glow: 0xF1C40F }   // Warlock
    };

    var colors = classColors[classType] || classColors[1];

    guardian = new THREE.Group();

    var bodyMat = new THREE.MeshStandardMaterial({
        color: colors.primary,
        metalness: 0.6,
        roughness: 0.35
    });

    var armorMat = new THREE.MeshStandardMaterial({
        color: colors.secondary,
        metalness: 0.85,
        roughness: 0.25
    });

    var glowMat = new THREE.MeshStandardMaterial({
        color: colors.glow,
        emissive: colors.glow,
        emissiveIntensity: 0.5
    });

    // Body
    var body = new THREE.Mesh(new THREE.CylinderGeometry(22, 28, 70, 8), bodyMat);
    body.position.y = 80;
    guardian.add(body);

    // Chest plate
    var chest = new THREE.Mesh(new THREE.BoxGeometry(45, 55, 8), armorMat);
    chest.position.set(0, 90, 18);
    guardian.add(chest);

    // Head
    var head = new THREE.Mesh(new THREE.SphereGeometry(16, 16, 12), armorMat);
    head.position.y = 140;
    guardian.add(head);

    // Visor
    var visor = new THREE.Mesh(new THREE.BoxGeometry(28, 7, 4), glowMat);
    visor.position.set(0, 140, 14);
    guardian.add(visor);

    // Legs
    var leftLeg = new THREE.Mesh(new THREE.CylinderGeometry(9, 11, 55, 6), bodyMat);
    leftLeg.position.set(-13, 22, 0);
    guardian.add(leftLeg);

    var rightLeg = leftLeg.clone();
    rightLeg.position.set(13, 22, 0);
    guardian.add(rightLeg);

    // Arms
    var leftArm = new THREE.Mesh(new THREE.CylinderGeometry(6, 7, 45, 6), bodyMat);
    leftArm.position.set(-38, 90, 0);
    leftArm.rotation.z = 0.1;
    guardian.add(leftArm);

    var rightArm = leftArm.clone();
    rightArm.position.set(38, 90, 0);
    rightArm.rotation.z = -0.1;
    guardian.add(rightArm);

    // Shoulders
    var leftShoulder = new THREE.Mesh(new THREE.SphereGeometry(12, 8, 6), armorMat);
    leftShoulder.position.set(-38, 115, 0);
    guardian.add(leftShoulder);

    var rightShoulder = leftShoulder.clone();
    rightShoulder.position.set(38, 115, 0);
    guardian.add(rightShoulder);

    // Feet
    var leftFoot = new THREE.Mesh(new THREE.BoxGeometry(14, 8, 22), armorMat);
    leftFoot.position.set(-13, -8, 4);
    guardian.add(leftFoot);

    var rightFoot = leftFoot.clone();
    rightFoot.position.set(13, -8, 4);
    guardian.add(rightFoot);

    scene.add(guardian);
    showLoading(false);
    isLoading = false;

    console.log('[Guardian3D] Placeholder shown for class:', classType);
}

function updateProgress(text) {
    var el = document.getElementById('loadingProgress');
    if (el) el.textContent = text;
}

function showLoading(show) {
    document.getElementById('loading').style.display = show ? 'block' : 'none';
}

function showError(msg) {
    document.getElementById('error').style.display = 'block';
    document.getElementById('error-message').textContent = msg;
}

function hideError() {
    document.getElementById('error').style.display = 'none';
}

// WebView2 listener
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        var data = event.data;
        console.log('[Guardian3D] Received from WPF:', data);

        if (data.action === 'loadGuardian') {
            loadGuardian(data.config);
        }
    });
}

window.loadGuardian = loadGuardian;
window.showPlaceholder = showPlaceholder;

document.addEventListener('DOMContentLoaded', function () {
    init();
    setTimeout(function () { showPlaceholder(1); }, 200);
});
