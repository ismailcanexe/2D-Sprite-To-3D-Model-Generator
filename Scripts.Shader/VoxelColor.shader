
Shader "Custom/VoxelLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Genel Ton", Color) = (1,1,1,1)

        [Toggle] _UseTexture ("Texture Kullan", Float) = 1
        [Toggle] _UseVertexColor ("Vertex Color Kullan", Float) = 0

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

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        fixed4 _Color;
        half _UseTexture;
        half _UseVertexColor;
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
