Shader "Custom/Raymarch"
{
    Properties
    {
        _VolumeTex ("3D Texture", 3D) = "white" {}
        _StepCount ("Step Count", Range(8, 256)) = 64
        _Density ("Density", Range(0.1, 10)) = 5.0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            sampler3D _VolumeTex;
            float _StepCount;
            float _Density;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Get object space position (cube from -0.5 to 0.5)
                o.objectPos = v.vertex.xyz;
                
                // Calculate view direction in object space
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = worldPos - _WorldSpaceCameraPos;
                o.viewDir = mul(unity_WorldToObject, float4(o.viewDir, 0)).xyz;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize to 0-1 range for 3D texture sampling
                float3 startPos = i.objectPos + 0.5;
                float3 viewDir = normalize(i.viewDir);
                
                // Raymarching parameters
                float stepSize = 1.732 / _StepCount; // Diagonal of unit cube
                float3 currentPos = startPos;
                float4 accumulated = float4(0, 0, 0, 0);
                
                [loop]
                for (int i = 0; i < _StepCount; i++) {
                    // Break if alpha is saturated
                    if (accumulated.a > 0.95) break;
                    
                    // Sample volume texture
                    float4 sample = tex3D(_VolumeTex, currentPos);
                    
                    // Calculate absorption using alpha channel for density
                    float density = sample.a * _Density;
                    float absorption = exp(-density * stepSize);
                    
                    // Accumulate color
                    accumulated.rgb += accumulated.a * sample.rgb * stepSize;
                    accumulated.a += (1 - accumulated.a) * (1 - absorption);
                    
                    // Advance position
                    currentPos += viewDir * stepSize;
                    
                    // Early exit if outside volume
                    if (any(currentPos < 0) || any(currentPos > 1)) break;
                }
                return accumulated;
            }
            ENDCG
        }
    }
}