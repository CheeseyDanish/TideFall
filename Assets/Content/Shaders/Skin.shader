// TideWall 3D — weapon skins (Players/Skins.cs): TideWall/Surface's lighting and baked shading, plus a pattern worked out
// in object space from a few numbers, so a skin needs no texture and stays put on a moving gun. Its properties sit
// outside the SRP batcher's block on purpose: only a few weapons are ever drawn with it.
Shader "TideWall/Skin"
{
    Properties
    {
        [MainTexture] _BaseMap("Base map", 2D) = "white" {}
        [MainColor] _BaseColor("Colour A", Color) = (1,1,1,1)
        _ColorB("Colour B", Color) = (0,0,0,1)
        [HDR] _Glow("Glow", Color) = (0,0,0,1)
        _Pattern("Pattern", Float) = 1
        _Scale("Pattern scale", Float) = 10
        _Pulse("Glow pulse speed", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = .4
        _Metallic("Metallic", Range(0,1)) = .3
        _Cutoff("Cutoff", Range(0,1)) = .5
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            float4 _ColorB, _Glow;
            float _Pattern, _Scale, _Pulse;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            float Hash(float3 p) { p = frac(p * .3183099 + .1); p *= 17; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            // How much of colour B (0..1) and of the glow at this point. Numbers match SkinPattern in GameDatabase.cs.
            float Pattern(float3 p, out float glow)
            {
                glow = 0;
                p *= _Scale;
                float k = _Pattern;
                if (k < 1.5) return 0;                                                                   // Solid
                if (k < 2.5) return smoothstep(.72, .78, Noise(p * .8) + .5 * Noise(p * 2.1 + 7));       // Camo
                if (k < 3.5) return step(.2, sin((p.z + p.y * .6 + Noise(p * .7) * 2.4) * 2.6));         // Stripes
                if (k < 4.5) return smoothstep(.58, .62, Noise(p * 1.3));                                // Spots
                if (k < 5.5)                                                                             // Veins
                {
                    float vein = 1 - smoothstep(0, .05, abs(Noise(p * .9) + .35 * Noise(p * 2.7) - .68));
                    glow = vein * (.65 + .35 * sin(_Time.y * _Pulse + p.z * .6));
                    return vein;
                }
                if (k < 6.5) return step(0, sin(p.z * 1.8 + sin(p.y * 2.4 + p.x) * 1.4));                // Waves
                if (k < 7.5) return step(.5, frac((p.z + abs(p.y)) * .35));                              // Hazard
                if (k < 8.5)                                                                             // Scales
                {
                    float row = floor(p.y * 1.4);
                    float2 f = frac(float2(p.z + row * .5, p.y * 1.4)) - .5;
                    return smoothstep(.28, .5, length(f * float2(1, 1.25)));
                }
                return 0;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float glow;
                float b = Pattern(i.positionOS, glow);
                float3 n = normalize(i.normalWS), view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 colour = lerp(_BaseColor.rgb, _ColorB.rgb, b);
                if (_Pattern > 8.5)                                                                      // Pearl: the colour shifts with the angle
                    colour *= .72 + .42 * cos(6.283 * (dot(n, view) * 1.3 + i.positionOS.z * .9 + float3(0, .33, .67)));

                SurfaceData s = (SurfaceData)0;
                s.albedo = colour * i.color.rgb;
                s.alpha = 1;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.emission = _Glow.rgb * glow;

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = view;
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1), ComputeFogFactor(i.positionCS.z));
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1,1,1,1);

                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, d.fogCoord);
                c.a = 1;
                return c;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
