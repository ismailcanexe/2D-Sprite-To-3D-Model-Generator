
Shader "Custom/VoxelLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _HeightMap ("Height Map", 2D) = "black" {}
        _Color ("Genel Ton", Color) = (1,1,1,1)

        [Toggle] _UseTexture ("Texture Kullan", Float) = 1
        [Toggle] _UseVertexColor ("Vertex Color Kullan", Float) = 0
        [Toggle] _UseNormalMap ("Normal Map Kullan", Float) = 0
        [Toggle] _UseHeightMap ("Height Map Kullan", Float) = 0
        [Toggle] _UseParallax ("Parallax Kullan", Float) = 0
        _NormalStrength ("Normal Strength", Range(0,4)) = 1
        _HeightAffect ("Height Affect", Range(0,1)) = 0.25
        _HeightNormalStrength ("Height Normal Strength", Range(0,8)) = 2
        _ParallaxStrength ("Parallax Strength", Range(0,0.1)) = 0.02

        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.0

        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _HeightMap;
        float4 _HeightMap_TexelSize;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 viewDir;
        };

        fixed4 _Color;
        half _UseTexture;
        half _UseVertexColor;
        half _UseNormalMap;
        half _UseHeightMap;
        half _UseParallax;
        half _NormalStrength;
        half _HeightAffect;
        half _HeightNormalStrength;
        half _ParallaxStrength;
        half _Metallic;
        half _Smoothness;
        fixed4 _EmissionColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;

            fixed baseHeight = tex2D(_HeightMap, uv).r;
            float3 viewDirTangent = normalize(IN.viewDir);
            float2 parallaxOffset = (viewDirTangent.xy / max(viewDirTangent.z, 0.2)) * ((baseHeight - 0.5) * _ParallaxStrength * saturate(_UseParallax) * saturate(_UseHeightMap));
            uv += parallaxOffset;

            fixed4 texCol = tex2D(_MainTex, uv);
            fixed4 vCol = IN.color;

            fixed4 textureLayer = lerp(fixed4(1,1,1,1), texCol, saturate(_UseTexture));
            fixed4 vertexLayer = lerp(fixed4(1,1,1,1), vCol, saturate(_UseVertexColor));
            fixed4 c = textureLayer * vertexLayer * _Color;

            fixed heightV = tex2D(_HeightMap, uv).r;
            fixed heightShade = lerp(1.0, lerp(0.85, 1.15, heightV), saturate(_UseHeightMap) * _HeightAffect);
            c.rgb *= heightShade;

            fixed3 nrm = UnpackNormal(tex2D(_NormalMap, uv));
            nrm.xy *= _NormalStrength;
            nrm.z = sqrt(saturate(1.0 - dot(nrm.xy, nrm.xy)));

            float hL = tex2D(_HeightMap, uv - float2(_HeightMap_TexelSize.x, 0)).r;
            float hR = tex2D(_HeightMap, uv + float2(_HeightMap_TexelSize.x, 0)).r;
            float hD = tex2D(_HeightMap, uv - float2(0, _HeightMap_TexelSize.y)).r;
            float hU = tex2D(_HeightMap, uv + float2(0, _HeightMap_TexelSize.y)).r;

            float2 heightGrad = float2(hL - hR, hD - hU) * _HeightNormalStrength;
            fixed3 heightNormal = normalize(fixed3(heightGrad.x, heightGrad.y, 1));

            fixed3 finalNormal = lerp(fixed3(0,0,1), nrm, saturate(_UseNormalMap));
            finalNormal = normalize(fixed3(finalNormal.xy + heightNormal.xy * saturate(_UseHeightMap) * _HeightAffect, finalNormal.z));
            o.Normal = finalNormal;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
