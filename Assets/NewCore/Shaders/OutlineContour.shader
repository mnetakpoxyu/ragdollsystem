Shader "NewCore/Outline Contour"
{
    Properties
    {
        [Header(Outline)]
        _OutlineWidth ("Outline Width (pixels)", Range(0.5, 8)) = 2.5
        _OutlineColor ("Outline Color", Color) = (0.2, 0.9, 0.4, 1)
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
            // Чуть отодвинуть обводку назад, чтобы не было артефактов на гранях
            Offset 1, 1

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
                // Позиция в clip space
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                // Нормаль в view space — направление обводки от камеры
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                // Толщина обводки в пикселях: ровная линия на экране
                float pixelScale = _OutlineWidth * clipPos.w * (1.0 / _ScreenParams.y);
                clipPos.xy += viewNormal.xy * pixelScale;
                o.pos = clipPos;
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
