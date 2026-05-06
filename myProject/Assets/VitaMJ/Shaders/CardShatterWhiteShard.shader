// 内置管线 · 卡牌碎裂粒子：硬边灰白色碎玻璃/瓷片观感（顶点色 × 透明度动画）
Shader "VitaMJ/Particles/WhiteShardUnlit"
{
    Properties
    {
        _Color ("整体染色", Color) = (0.94, 0.95, 0.97, 1)
        _CenterBright ("中心提亮", Range(0.95, 1.65)) = 1.28
        _BorderDark ("边缘压暗", Range(0.12, 0.55)) = 0.34
        _Sharpness ("边缘硬度（越大越利）", Range(4, 80)) = 36
        _Aspect ("碎片细长比", Range(0.55, 1.9)) = 1.08
        _Rough ("周缘断裂分段", Range(1.8, 6.5)) = 3.4
        _Cut ("边缘起伏幅度", Range(0.2, 0.85)) = 0.42
        _MainTex ("占位（粒子 UV）", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
                float4 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float4 uvSeed : TEXCOORD0;
            };

            sampler2D _MainTex;

            fixed4 _Color;
            half _CenterBright;
            half _BorderDark;
            half _Sharpness;
            half _Aspect;
            half _Rough;
            half _Cut;

            float Hash11(float x)
            {
                return frac(sin(x * 127.891) * 43758.5453);
            }

            // 负值为形内 · 正值为形外（与圆角长方 SDF 一致）
            float SdRoundedBox(float2 p, float2 halfExt, float cornerR)
            {
                float2 d = abs(p) - halfExt;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - cornerR;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPivot = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                half seed =
                    saturate(frac(
                        dot(worldPivot + float3(0.731, v.vertex.x, -v.vertex.y), float3(12.9898, 78.233, 37.719)) *
                        43758.5453));

                float ang = seed * 6.2831853;
                half sna, cosa;
                sincos(ang, sna, cosa);

                float2 uvo = v.texcoord.xy * 2.0 - 1.0;
                float2 uvr = mul(float2x2(cosa, -sna, sna, cosa), uvo);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uvSeed.xy = float2(uvr.x * 0.6 + seed * 0.4 + sna * 0.06, uvr.y);
                o.uvSeed.z = seed;
                o.uvSeed.w = Hash11(seed * 99.731 + dot(worldPivot, float3(1, 7, 3)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half seed01 = saturate(i.uvSeed.z);
                half seedAlt = saturate(i.uvSeed.w);

                float2 u = float2(i.uvSeed.x, i.uvSeed.y * _Aspect);
                float ang = atan2(u.y, u.x);
                float seg = floor(ang * (_Rough + 3.5) + seedAlt * 6.2831853);
                float wobble = (Hash11(seg * 3.17 + seed01 * 41.0) - 0.5) * _Cut * 0.11;
                float stretch = 0.78 + seed01 * 0.36;
                float2 ext = float2(0.31 + wobble, 0.44 - wobble * 0.35) * stretch;
                float corner = 0.018 + seedAlt * 0.028;
                float d = SdRoundedBox(u, ext, corner);
                float edgePx = (6.0 / max(_Sharpness, 4.0)) + fwidth(d) * 2.5;
                float mask = saturate(1.0 - smoothstep(-edgePx, edgePx, d));
                float edgeFactor = saturate(1.0 - mask * mask);

                half3 core = half3(0.94, 0.95, 0.98);
                half3 rim = half3(0.72, 0.73, 0.76);
                half3 alb = lerp(rim, core + (_CenterBright - 1.0), mask);
                alb = lerp(alb - _BorderDark * 0.85, alb, mask);
                alb = saturate(alb);

                fixed4 texel = tex2D(_MainTex, float2(i.uvSeed.x, i.uvSeed.y) * 0.35 + 0.5);
                half a = saturate(mask * (0.88 + edgeFactor * 0.12));
                fixed4 o;
                o.rgb = alb * i.color.rgb * texel.rgb;
                o.a = a * i.color.a * texel.a;
                return o;
            }

            ENDCG
        }
    }

    Fallback Off
}