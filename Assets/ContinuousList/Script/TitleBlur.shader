Shader "Custom/TitleBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurAmount ("Blur Amount", Range(0, 1)) = 0
        // Expose the max blur radius for easier tuning in the Inspector
        _MaxBlurRadius ("Max Blur Radius", Int) = 20 

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_TexelSize;
            float _BlurAmount;
            int _MaxBlurRadius; // Maximum pixel radius for the blur

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                // Get the original UI color, including tint and add
                half4 originalColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                originalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (originalColor.a - 0.001);
                #endif

                if (_BlurAmount <= 0.01) // Early exit for no blur
                {
                    return originalColor;
                }

                // Calculate the dynamic kernel radius based on _BlurAmount
                int radius = (int)(_BlurAmount * _MaxBlurRadius);
                if (radius < 1) radius = 1; // Ensure radius is at least 1

                half4 blurredColor = half4(0,0,0,0);
                int sampleCount = 0;

                for (int x = -radius; x <= radius; ++x)
                {
                    for (int y = -radius; y <= radius; ++y)
                    {
                        // Simple box blur: all samples have equal weight
                        float2 offset = float2(x * _MainTex_TexelSize.x, y * _MainTex_TexelSize.y);
                        // Sample the raw texture, then apply color/tint later
                        blurredColor += tex2D(_MainTex, IN.texcoord + offset);
                        sampleCount++;
                    }
                }
                
                // Average the samples
                blurredColor /= sampleCount;
                
                // Apply original color and tint to the blurred texture
                blurredColor = (blurredColor + _TextureSampleAdd) * IN.color;
                
                // Lerp between original color and blurred color based on _BlurAmount
                return lerp(originalColor, blurredColor, _BlurAmount);
            }
            ENDCG
        }
    }
}
