Shader "Caronte/Digital Reveal"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _RevealColor("Reveal Color", Color) = (0, 1, 1, 1)
        _RevealPosition("Reveal Position", Float) = 0
        _RevealDirection("Reveal Direction", Vector) = (0, 1, 0, 0)
        _RevealEdgeWidth("Reveal Edge Width", Float) = 0.05
        _RevealSoftness("Reveal Softness", Float) = 0.015
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "DigitalReveal"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RevealColor;
                float _RevealPosition;
                float4 _RevealDirection;
                float _RevealEdgeWidth;
                float _RevealSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(textureColor.a - 0.005h);

                float scanCoordinate = dot(input.positionWS, normalize(_RevealDirection.xyz));
                float revealMask = 1.0 - smoothstep(
                    _RevealPosition - _RevealSoftness,
                    _RevealPosition + _RevealSoftness,
                    scanCoordinate);
                float edge = 1.0 - smoothstep(
                    0.0,
                    max(0.0001, _RevealEdgeWidth),
                    abs(scanCoordinate - _RevealPosition));
                clip(revealMask - 0.02);

                half3 revealedColor = textureColor.rgb * _BaseColor.rgb;
                half3 finalColor = revealedColor;
                finalColor += _RevealColor.rgb * edge * 2.5h;
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
