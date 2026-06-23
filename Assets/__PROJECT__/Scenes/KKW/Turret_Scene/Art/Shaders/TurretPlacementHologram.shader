Shader "Project Z Defense/Placement/Turret Hologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.55, 1.0, 0.62)
        _RimColor ("Rim Color", Color) = (0.34, 0.95, 1.0, 1.0)
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.72
        _RimPower ("Rim Power", Range(0.25, 8.0)) = 1.35
        _RimStrength ("Rim Strength", Range(0.0, 4.0)) = 2.7
        _ScanlineScale ("Scanline Scale", Range(1.0, 80.0)) = 10.0
        _ScanlineSpeed ("Scanline Speed", Range(-8.0, 8.0)) = 1.7
        _ScanlineStrength ("Scanline Strength", Range(0.0, 1.0)) = 0.82
        _PulseSpeed ("Pulse Speed", Range(0.0, 8.0)) = 1.2
        _PulseStrength ("Pulse Strength", Range(0.0, 1.0)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PlacementHologram"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _Alpha;
                half _RimPower;
                half _RimStrength;
                half _ScanlineScale;
                half _ScanlineSpeed;
                half _ScanlineStrength;
                half _PulseSpeed;
                half _PulseStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                rim = saturate(rim);

                half scanPosition = frac((input.positionWS.y * _ScanlineScale) - (_Time.y * _ScanlineSpeed));
                half scanDistance = abs(scanPosition - 0.5h);
                half scanWide = 1.0h - smoothstep(0.14h, 0.32h, scanDistance);
                half scanThin = 1.0h - smoothstep(0.0h, 0.035h, scanDistance);
                half scan = saturate((scanWide * 0.35h + scanThin) * _ScanlineStrength);

                half pulse = 1.0h + ((sin(_Time.y * _PulseSpeed) * 0.5h + 0.5h) * _PulseStrength);
                half glow = saturate(rim + (scan * 0.85h)) * pulse;

                half3 color = (_BaseColor.rgb * (0.7h + pulse * 0.35h)) + (_RimColor.rgb * glow);
                half alpha = saturate((_BaseColor.a * _Alpha) + (rim * 0.32h) + (scan * 0.24h));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
