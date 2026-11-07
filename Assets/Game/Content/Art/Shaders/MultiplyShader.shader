Shader "Unlit/MultiplyShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _LightmapTexelSize ("Lightmap texel size", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        // 1. CHANGED: Switched from Opaque to Transparent queue
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        // 2. ADDED: This is the magic blend mode that forces MULTIPLY
        Blend Zero SrcColor 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // REMOVED: Fog multi-compile line is gone

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                // REMOVED: Fog coordinates line is gone
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _LightmapTexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // REMOVED: Fog transfer line is gone
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample occupancy at the texel center so bilinear filtering cannot
                // extend the foreground overlay into air. White is neutral for multiply.
                // UI can cache _MainTex_TexelSize across in-place texture resizes.
                float4 texelSize = _LightmapTexelSize.z > 0 ? _LightmapTexelSize : _MainTex_TexelSize;
                float2 maskUV = (floor(i.uv * texelSize.zw) + 0.5) * texelSize.xy;
                if (tex2D(_MainTex, maskUV).a < 0.5)
                    return fixed4(1, 1, 1, 1);

                // RGB contains masked light. Normalize by interpolated coverage to
                // avoid a dark fringe where solid texels meet transparent ones.
                float4 col = tex2D(_MainTex, i.uv);
                return fixed4(col.rgb / max(col.a, 0.0001), 1);
            }
            ENDCG
        }
    }
}
