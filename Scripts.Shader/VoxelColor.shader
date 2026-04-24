
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
        _NormalStrength ("Normal Strength", Range(0,4)) = 1
        _HeightAffect ("Height Affect", Range(0,1)) = 0.25

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

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        fixed4 _Color;
        half _UseTexture;
        half _UseVertexColor;
        half _UseNormalMap;
        half _UseHeightMap;
        half _NormalStrength;
        half _HeightAffect;
        half _Metallic;
        half _Smoothness;
        fixed4 _EmissionColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 texCol = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 vCol = IN.color;

            fixed4 textureLayer = lerp(fixed4(1,1,1,1), texCol, saturate(_UseTexture));
            fixed4 vertexLayer = lerp(fixed4(1,1,1,1), vCol, saturate(_UseVertexColor));
            fixed4 c = textureLayer * vertexLayer * _Color;

            fixed heightV = tex2D(_HeightMap, IN.uv_MainTex).r;
            fixed heightShade = lerp(1.0, lerp(0.85, 1.15, heightV), saturate(_UseHeightMap) * _HeightAffect);
            c.rgb *= heightShade;

            fixed3 nrm = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
            nrm.xy *= _NormalStrength;
            nrm.z = sqrt(saturate(1.0 - dot(nrm.xy, nrm.xy)));
            o.Normal = normalize(lerp(fixed3(0,0,1), nrm, saturate(_UseNormalMap)));

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
