/**
 * Guardian 3D Viewer - D2 Compatible
 * Uses D2TGXLoader for real Destiny 2 models via local proxy
 */

let camera, scene, renderer, controls;
let guardian = null;
let clock = new THREE.Clock();
let isLoading = false;

function init() {
    const container = document.getElementById('container');

    scene = new THREE.Scene();

    camera = new THREE.PerspectiveCamera(30, container.clientWidth / container.clientHeight, 0.1, 10000);
    camera.position.set(0, 150, 800);
    camera.lookAt(0, 100, 0);
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
    container.appendChild(renderer.domElement);

    setupLighting();

    controls = new THREE.OrbitControls(camera, renderer.domElement);
    controls.rotateSpeed = 1.2;
    controls.zoomSpeed = 0.6;
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.enablePan = false;
    controls.minDistance = 300;
    controls.maxDistance = 1500;
    controls.target.set(0, 100, 0);

    window.addEventListener('resize', onWindowResize, false);
    animate();

    console.log('[Guardian3D] Viewer initialized');
}

function setupLighting() {
    scene.add(new THREE.AmbientLight(0x404060, 0.6));

    var keyLight = new THREE.DirectionalLight(0xfff5e0, 1.3);
    keyLight.position.set(300, 500, 400);
    scene.add(keyLight);

    var fillLight = new THREE.DirectionalLight(0xd0e0ff, 0.5);
    fillLight.position.set(-300, 300, 300);
    scene.add(fillLight);

    var rimLight = new THREE.DirectionalLight(0xffffff, 0.4);
    rimLight.position.set(0, 200, -500);
    scene.add(rimLight);

    var bottomLight = new THREE.PointLight(0x9D4EDD, 0.6, 400);
    bottomLight.position.set(0, -100, 100);
    scene.add(bottomLight);
}

function onWindowResize() {
    const container = document.getElementById('container');
    camera.aspect = container.clientWidth / container.clientHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(container.clientWidth, container.clientHeight);
}

function animate() {
    requestAnimationFrame(animate);

    var elapsed = clock.getElapsedTime();

    if (guardian) {
        guardian.scale.y = 1 + Math.sin(elapsed * 1.5) * 0.002;
    }

    controls.update();
    renderer.render(scene, camera);
}

/**
 * Load guardian using D2TGXLoader
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
    var classType = config.classType || 1;
    var isFemale = config.isFemale || false;

    if (itemHashes.length === 0) {
        console.warn('[Guardian3D] No item hashes, showing placeholder');
        showPlaceholder(classType);
        return;
    }

    console.log('[Guardian3D] Loading guardian with D2TGXLoader');
    console.log('[Guardian3D] Items:', itemHashes);

    // Use our new D2TGXLoader
    if (window.D2TGXLoader) {
        try {
            const loader = new D2TGXLoader();

            const mesh = await loader.load({
                itemHashes: itemHashes,
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
                // Scale and position the model
                mesh.rotation.x = -(90 * Math.PI / 180);
                mesh.scale.set(100, 100, 100);

                guardian = mesh;
                scene.add(guardian);

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
