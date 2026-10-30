Shader "SketchLand/MaskedDamage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Object Alpha Mask", 2D) = "white" {}
        _CrackTex ("Crack Texture", 2D) = "white" {}
        _CrackRect ("Crack Atlas UV Rect", Vector) = (0,0,1,1)
        _CrackFrame ("Crack Crop Within Original Canvas", Vector) = (0,0,1,1)
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _CrackTex;
            float4 _CrackTex_TexelSize;
            float4 _CrackRect;
            float4 _CrackFrame;
            fixed4 _Color;
            struct Attributes
            {
                float4 position : POSITION;
                float2 maskUV : TEXCOORD0;
                float2 cellUV : TEXCOORD1;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 maskUV : TEXCOORD0;
                float2 cellUV : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.maskUV = input.maskUV;
                output.cellUV = input.cellUV;
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                // Preserve the transparent padding in each stage's original canvas.
                float2 cropUV = (frac(input.cellUV) - _CrackFrame.xy)
                    / max(_CrackFrame.zw, float2(0.00001, 0.00001));
                float inside = step(0, cropUV.x) * step(cropUV.x, 1)
                    * step(0, cropUV.y) * step(cropUV.y, 1);
                // Clamp filtering to this stage without rescaling its artwork.
                float2 inset = _CrackTex_TexelSize.xy * 0.5;
                float2 crackUV = clamp(_CrackRect.xy + cropUV * _CrackRect.zw,
                    _CrackRect.xy + inset, _CrackRect.xy + _CrackRect.zw - inset);
                fixed4 crack = tex2D(_CrackTex, crackUV) * _Color;
                crack.a *= inside * tex2D(_MainTex, input.maskUV).a;
                return crack;
            }
            ENDCG
        }
    }
}
