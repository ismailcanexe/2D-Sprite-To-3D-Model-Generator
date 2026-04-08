Shader "Custom/VoxelLit"
{
    Properties
    {
        // İstersen materyal üzerinden genel bir renk tonu verebilirsin (Normalde beyaz kalmalı)
        _Color ("Genel Ton", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // "Standard" aydınlatma modelini kullan (Işık ve gölgeleri aktif eder)
        #pragma surface surf Standard fullforwardshadows

        #pragma target 3.0

        // Objenin datalarından köşe rengini (Vertex Color) içeri alıyoruz
        struct Input
        {
            float4 color : COLOR; 
        };

        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Köşe rengi (orijinal piksel rengi) ile genel tonu çarp
            fixed4 c = IN.color * _Color;
            
            // Albedo objenin ana rengidir
            o.Albedo = c.rgb;
            
            // Minecraft tarzı eşyalar mat olur. Parlamasını istersen bu değerleri artırabilirsin.
            o.Metallic = 0.0; 
            o.Smoothness = 0.0;
            o.Alpha = c.a;
        }
        ENDCG
    }
    // Unity gölge oluşturmak için bu FallBack'e ihtiyaç duyar
    FallBack "Diffuse" 
}