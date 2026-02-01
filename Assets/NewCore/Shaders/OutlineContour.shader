Shader "NewCore/Outline Contour"
{
    Properties
    {
        [Header(Outline)]
        _OutlineWidth ("Outline Width", Range(0.02, 0.25)) = 0.08
        _OutlineColor ("Outline Color", Color) = (0.15, 1, 0.4, 1)
        _RGBSpeed ("RGB Cycle (0 = solid)", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            Offset 2, 2

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _OutlineWidth;
            float4 _OutlineColor;
            float _RGBSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float3 worldNorm = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                // По силуэту: не расширяем «перед» объекта — нет повторения в первом лице
                float towardCamera = dot(viewDir, worldNorm);
                float silhouette = 1.0 - saturate(towardCamera);
                float dist = length(worldPos.xyz - _WorldSpaceCameraPos);
                float distScale = lerp(0.4, 1.0, saturate((dist - 0.5) / 2.5));
                worldPos.xyz += worldNorm * (_OutlineWidth * max(0.15, silhouette) * distScale);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                return o;
            }

            fixed3 hueToRgb(float h)
            {
                fixed r = abs(h * 6 - 3) - 1;
                fixed g = 2 - abs(h * 6 - 2);
                fixed b = 2 - abs(h * 6 - 4);
                return saturate(fixed3(r, g, b));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_RGBSpeed > 0)
                {
                    float hue = frac(_Time.y * _RGBSpeed);
                    return fixed4(hueToRgb(hue), 1);
                }
                return _OutlineColor;
            }
            ENDCG
        }
    }
    Fallback "Off"
}
