Shader "Custom/SpotlightOverlay"
{
    Properties
    {
        _SpotlightCenter ("Spotlight Center", Vector) = (0.5, 0.5, 0, 0)
        _SpotlightRadius ("Spotlight Radius", Float) = 0.15
        _SoftEdgeWidth ("Soft Edge Width", Float) = 0.04
        _DarknessAlpha ("Darkness Alpha", Float) = 0.92
        _AspectRatio ("Aspect Ratio", Float) = 1.7778
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SpotlightOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _SpotlightCenter;
            float _SpotlightRadius;
            float _SoftEdgeWidth;
            float _DarknessAlpha;
            float _AspectRatio;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UV를 뷰포트 좌표로 사용 (0~1)
                float2 uv = IN.uv;
                float2 center = _SpotlightCenter.xy;

                // 종횡비 보정: UV 공간에서 X를 늘려서 원이 찌그러지지 않게
                float2 diff = uv - center;
                diff.x *= _AspectRatio;

                float dist = length(diff);

                // smoothstep: radius 안쪽=0(투명), radius+softEdge 바깥=1(어둡게)
                float darkness = smoothstep(_SpotlightRadius, _SpotlightRadius + _SoftEdgeWidth, dist);

                return half4(0, 0, 0, darkness * _DarknessAlpha);
            }
            ENDHLSL
        }
    }
}
