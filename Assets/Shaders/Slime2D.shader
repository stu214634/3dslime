Shader "Custom/Slime2D"
{
    Properties
    {
        _SlimeTexture ("Slime Texture", 2D) = "black" {}
        _Intensity ("Intensity", Range(0.5, 10)) = 2.0
        _Color ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.0
        _TextureScale ("Texture Scale", Range(0.1, 5)) = 1.0
        _MappingMode ("Mapping Mode", Float) = 0
        _HeightScale ("Height Scale", Range(0, 0.2)) = 0.05
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _ParallaxSteps ("Parallax Steps", Range(8, 64)) = 32
        _ParallaxDepth ("Parallax Depth", Range(0, 0.1)) = 0.03
        [HDR] _SpeciesColor1 ("Species Color 1", Color) = (1,0,0,1)
        [HDR] _SpeciesColor2 ("Species Color 2", Color) = (0,1,0,1)
        [HDR] _SpeciesColor3 ("Species Color 3", Color) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float3 viewDirTS : TEXCOORD4;
            };

            TEXTURE2D(_SlimeTexture);
            SAMPLER(sampler_SlimeTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SlimeTexture_ST;
                float _Intensity;
                float4 _Color;
                float _Metallic;
                float _Smoothness;
                float _EmissionStrength;
                float _TextureScale;
                float _MappingMode;
                float _HeightScale;
                float _NormalStrength;
                float _ParallaxSteps;
                float _ParallaxDepth;
                float4 _SpeciesColor1;
                float4 _SpeciesColor2;
                float4 _SpeciesColor3;
            CBUFFER_END

            // Parallax Occlusion Mapping function
            float2 ParallaxOcclusionMapping(float2 texCoords, float3 viewDirTS)
            {
                // Number of depth layers
                float numLayers = lerp(_ParallaxSteps, _ParallaxSteps * 0.5, abs(dot(float3(0, 0, 1), viewDirTS)));
                
                // Calculate the size of each layer
                float layerDepth = 1.0 / numLayers;
                float currentLayerDepth = 0.0;
                
                // The amount to shift the texture coordinates per layer (from vector P)
                float2 P = viewDirTS.xy * _ParallaxDepth;
                float2 deltaTexCoords = P / numLayers;
                
                // Get initial values
                float2 currentTexCoords = texCoords;
                float currentDepthMapValue = 1.0 - length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, currentTexCoords).rgb);
                
                // Ray marching
                [unroll(32)]
                for(int i = 0; i < numLayers && currentLayerDepth < currentDepthMapValue; i++)
                {
                    // Shift texture coordinates along direction of P
                    currentTexCoords -= deltaTexCoords;
                    // Get depthmap value at current texture coordinates
                    currentDepthMapValue = 1.0 - length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, currentTexCoords).rgb);
                    // Get depth of next layer
                    currentLayerDepth += layerDepth;
                }
                
                // Get texture coordinates before collision (reverse operations)
                float2 prevTexCoords = currentTexCoords + deltaTexCoords;
                
                // Get depth after and before collision for linear interpolation
                float afterDepth = currentDepthMapValue - currentLayerDepth;
                float beforeDepth = (1.0 - length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, prevTexCoords).rgb)) - currentLayerDepth + layerDepth;
                
                // Interpolation of texture coordinates
                float weight = afterDepth / (afterDepth - beforeDepth);
                float2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);
                
                return finalTexCoords;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _SlimeTexture);
                
                // Calculate view direction in tangent space for parallax mapping
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                float3 bitangentWS = cross(normalInputs.normalWS, normalInputs.tangentWS) * input.tangentOS.w;
                float3x3 worldToTangent = float3x3(normalInputs.tangentWS, bitangentWS, normalInputs.normalWS);
                output.viewDirTS = mul(worldToTangent, viewDirWS);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Calculate base texture coordinates based on mapping mode
                float2 baseTexCoords;
                
                if (_MappingMode < 0.5) // UV Mapping (Default)
                {
                    // Use UV coordinates for proper mesh mapping
                    baseTexCoords = input.uv * _TextureScale;
                }
                else // World Space Mapping
                {
                    // Use world space position for global texture mapping
                    baseTexCoords = input.positionWS.xy * _TextureScale * 0.1;
                }
                
                // Apply parallax occlusion mapping
                float2 texCoords = ParallaxOcclusionMapping(baseTexCoords, normalize(input.viewDirTS));
                
                // Sample the slime texture with parallax-adjusted coordinates
                half4 slimeSample = SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, texCoords);
                
                // Extract individual species channels
                half species1 = slimeSample.r;
                half species2 = slimeSample.g;
                half species3 = slimeSample.b;
                
                // Calculate species colors
                half3 speciesColor = species1 * _SpeciesColor1.rgb + 
                                   species2 * _SpeciesColor2.rgb + 
                                   species3 * _SpeciesColor3.rgb;
                
                // Calculate total slime intensity
                half totalIntensity = length(slimeSample.rgb) * _Intensity;
                
                // Calculate normal map from slime height
                float texelSize = 1.0 / 512.0; // Approximate texel size
                half heightL = length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, texCoords + float2(-texelSize, 0)).rgb);
                half heightR = length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, texCoords + float2(texelSize, 0)).rgb);
                half heightD = length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, texCoords + float2(0, -texelSize)).rgb);
                half heightU = length(SAMPLE_TEXTURE2D(_SlimeTexture, sampler_SlimeTexture, texCoords + float2(0, texelSize)).rgb);
                
                // Calculate normal from height differences
                half3 normalTS = normalize(half3(
                    (heightL - heightR) * _NormalStrength,
                    (heightD - heightU) * _NormalStrength,
                    1.0
                ));
                
                // Base surface properties
                half3 albedo = _Color.rgb * (1.0 + speciesColor * totalIntensity);
                half3 emission = speciesColor * totalIntensity * _EmissionStrength;
                
                // Transform normal to world space
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tangentToWorld));
                
                // Lighting calculation
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.emission = emission;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.alpha = 1.0;
                
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
} 