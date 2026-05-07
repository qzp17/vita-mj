// 内置管线 · 粒子碎块：法线明暗 + 噪声体积感；默认白色，仅靠浅灰暗部区分块面。
Shader "VitaMJ/Particles/RockChunkUnlit"
{
    Properties
    {
        _Color ("亮部", Color) = (1, 1, 1, 1)
        _Dark ("暗部（略浅灰，仍是白调）", Color) = (0.88, 0.88, 0.90, 1)
        _LightDir ("假光方向 (世界空间)", Vector) = (0.45, 0.85, 0.25, 0)
        _NoiseScale ("表面噪声尺度", Range(2, 48)) = 14
        _Contrast ("明暗对比", Range(0.35, 2.2)) = 0.88
        _Rim ("边缘提亮", Range(0, 0.55)) = 0.18
        _MainTex ("粒子纹理（可为 white）", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float3 normal : NORMAL;
                float4 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD0;
                float3 worldN : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            sampler2D _MainTex;

            fixed4 _Color;
            fixed4 _Dark;
            float4 _LightDir;
            half _NoiseScale;
            half _Contrast;
            half _Rim;

            float Hash31(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float4 wpos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityWorldToClipPos(wpos.xyz);
                o.worldPos = wpos.xyz;
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.worldN = normalize(length(n) > 1e-4 ? n : UnityWorldSpaceViewDir(wpos.xyz));
                o.color = v.color;
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldN);
                float3 L = normalize(_LightDir.xyz);
                float nd = saturate(dot(N, L));
                float wrap = nd * 0.72 + 0.28;

                float3 p = i.worldPos * (_NoiseScale * 0.1);
                float g =
                    Noise3(p) * 0.55 +
                    Noise3(p * 2.07 + 3.1) * 0.28 +
                    Noise3(p * 5.13 + 1.7) * 0.17;

                float tone = saturate(pow(wrap, _Contrast) * (0.72 + g * 0.52));

                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float rim = pow(1.0 - saturate(dot(N, V)), 2.2) * _Rim;

                fixed3 alb = lerp(_Dark.rgb, _Color.rgb, tone);
                alb += rim * fixed3(1.0, 1.0, 1.0);

                fixed4 texel = tex2D(_MainTex, i.uv);
                fixed4 o;
                o.rgb = alb * texel.rgb * i.color.rgb;
                o.a = texel.a * i.color.a;
                return o;
            }

            ENDCG
        }
    }

    Fallback Off
}
