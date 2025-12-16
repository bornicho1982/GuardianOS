// Custom ShaderMaterial that implements Destiny 2 gear dyes
// Adapted from lowlines/destiny-tgx-loader for GuardianOS
// Using ES6 class syntax for Three.js r128+ compatibility

(function () {
    // ES6 Class that extends THREE.ShaderMaterial
    class TGXMaterial extends THREE.ShaderMaterial {
        constructor(params = {}) {
            super({
                lights: true,
                fog: true
            });

            this.defaultAttributeValues.detailUv = [0, 0];
            this.side = THREE.DoubleSide;
            this.game = 'destiny2';

            this.map = null;
            this.normalMap = null;
            this.envMap = null;
            this.gearstackMap = null;

            this.color = new THREE.Color(0xffffff);
            this.emissive = new THREE.Color(0x000000);
            this.emissiveIntensity = 1;
            this.metalness = 0.5;
            this.roughness = 0.5;

            this.usePrimaryColor = true;
            this.primaryColor = new THREE.Color(0x000000);
            this.secondaryColor = new THREE.Color(0xFFFFFF);
            this.wornColor = new THREE.Color(0x666666);

            this.primaryParams = new THREE.Vector4(0, 0, 0, 0);
            this.secondaryParams = new THREE.Vector4(0, 0, 0, 0);
            this.wornParams = new THREE.Vector4(0, 0, 0, 0);

            this.useDye = true;

            this.setValues(params);
            this.extensions.derivatives = true;
            this.updateShaders();
        }

        updateShaders() {
            const shaderLib = THREE.ShaderLib.standard;
            const uniforms = THREE.UniformsUtils.clone(shaderLib.uniforms);
            let vertexShader = shaderLib.vertexShader;
            let fragmentShader = shaderLib.fragmentShader;
            const defines = {};

            if (this.map) {
                defines['USE_MAP'] = '';
                uniforms.map = { value: this.map };
            }

            if (this.color) {
                uniforms.diffuse = { value: this.color };
            }

            uniforms.metalness = { value: this.metalness };
            uniforms.roughness = { value: this.roughness };

            // Destiny specific uniforms
            uniforms.usePrimaryColor = { value: this.usePrimaryColor };
            uniforms.primaryColor = { value: this.primaryColor };
            uniforms.secondaryColor = { value: this.secondaryColor };
            uniforms.wornColor = { value: this.wornColor };
            uniforms.primaryParams = { value: this.primaryParams };
            uniforms.secondaryParams = { value: this.secondaryParams };

            if (this.gearstackMap) {
                defines['USE_GEARSTACKMAP'] = '';
                uniforms.gearstackMap = { value: this.gearstackMap };
            }

            defines['USE_DESTINY2'] = '';

            if (this.useDye) {
                defines['USE_DYE'] = '';
            }

            // Shader chunks - inject Destiny 2 dye system
            const chunks = TGXMaterial.ShaderChunk;

            // Add fragment shader modifications
            if (fragmentShader.indexOf('USE_GEARSTACKMAP') === -1) {
                fragmentShader = this.insertAfter('#include <map_pars_fragment>', fragmentShader, chunks.common_pars_fragment + chunks.gearstack_pars_fragment);
                fragmentShader = this.insertAfter('#include <map_fragment>', fragmentShader, chunks.gearstack_blend_fragment);
            }

            this.defines = defines;
            this.uniforms = uniforms;
            this.vertexShader = vertexShader;
            this.fragmentShader = fragmentShader;
        }

        insertAfter(search, shader, insertCode) {
            const searchWithNewline = search + "\n";
            const code = typeof insertCode === 'string' ? insertCode : insertCode.join("\n");
            return shader.replace(searchWithNewline, searchWithNewline + code + "\n");
        }
    }

    // Shader chunks for Destiny 2 dye system
    TGXMaterial.ShaderChunk = {
        common_pars_fragment: `
            const float gamma_correction_power = 2.2;
            const float gamma_correction_power_inverse = 1.0/2.2;
            
            vec4 blend_overlay(vec4 back, vec4 front) {
                return front * clamp(back * 4.0, 0.0, 1.0) + clamp(back - 0.25, 0.0, 1.0);
            }
            vec4 blend_multiply(vec4 back, vec4 front) {
                return back * front;
            }
        `,
        gearstack_pars_fragment: `
            #ifdef USE_GEARSTACKMAP
                uniform sampler2D gearstackMap;
            #endif
            
            #ifdef USE_DESTINY2
                uniform bool usePrimaryColor;
                uniform vec3 primaryColor;
                uniform vec3 secondaryColor;
                uniform vec3 wornColor;
                uniform vec4 primaryParams;
                uniform vec4 secondaryParams;
            #endif
        `,
        gearstack_blend_fragment: `
            #ifdef USE_DESTINY2
                // Apply gamma correction to diffuse
                diffuseColor = pow(diffuseColor, vec4(gamma_correction_power));
                
                // Get dye color based on slot
                vec4 dyeColor = usePrimaryColor ? vec4(primaryColor, 1.0) : vec4(secondaryColor, 1.0);
                
                // Since JPEG textures don't have alpha channel, we use a different approach:
                // Apply dye color to the entire surface using blend_overlay
                #ifdef USE_DYE
                    // Create a mask based on the brightness of the texture
                    // Brighter areas get more dye, darker areas (like cloth/leather) get less
                    float luminance = dot(diffuseColor.rgb, vec3(0.299, 0.587, 0.114));
                    float dyeStrength = 0.7; // How much the dye affects the base color
                    
                    // Blend the dye color with the base color
                    vec4 tintedColor = blend_overlay(diffuseColor, dyeColor);
                    diffuseColor = mix(diffuseColor, tintedColor, dyeStrength);
                #endif
                
                // Remove gamma correction
                diffuseColor = vec4(pow(diffuseColor.xyz, vec3(gamma_correction_power_inverse)), 1.0);
            #endif
        `
    };

    // Register globally
    THREE.TGXMaterial = TGXMaterial;
})();

console.log('[TGXMaterial] Loaded v2 (ES6 class)');
