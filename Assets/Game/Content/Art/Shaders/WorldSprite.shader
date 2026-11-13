Shader "SketchLand/WorldSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "WorldLighting.cginc"

            struct WorldVaryings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
            };

            WorldVaryings Vert(appdata_t input)
            {
                WorldVaryings output;
                v2f sprite = SpriteVert(input);
                output.vertex = sprite.vertex;
                output.color = sprite.color;
                output.texcoord = sprite.texcoord;
                // Match SpriteVert's renderer flip before mapping into the light field.
                float4 vertex = UnityFlipSprite(input.vertex, _Flip);
                output.worldPosition = mul(unity_ObjectToWorld, vertex).xy;
                return output;
            }

            fixed4 Frag(WorldVaryings input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                color.rgb *= color.a;
                color.rgb *= SampleWorldLight(input.worldPosition);
                return color;
            }
            ENDCG
        }
    }
}
