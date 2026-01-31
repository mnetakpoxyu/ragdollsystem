Shader "NewCore/Outline Contour"
{
    Properties
    {
        _OutlineWidth ("Outline Width", Range(0.01, 0.4)) = 0.24
        _RGBSpeed ("RGB Cycle Speed", Range(0.1, 2)) = 0.35
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

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _OutlineWidth;
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
                float3 norm = normalize(v.normal);
                float3 outlineVertex = v.vertex.xyz + norm * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(outlineVertex, 1));
                return o;
            }

            // Hue [0..1] -> RGB
            fixed3 hueToRgb(float h)
            {
                fixed r = abs(h * 6 - 3) - 1;
                fixed g = 2 - abs(h * 6 - 2);
                fixed b = 2 - abs(h * 6 - 4);
                return saturate(fixed3(r, g, b));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float hue = frac(_Time.y * _RGBSpeed);
                fixed3 rgb = hueToRgb(hue);
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
    Fallback "Off"
}
