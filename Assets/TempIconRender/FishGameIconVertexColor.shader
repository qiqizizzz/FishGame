Shader "Hidden/FishGame/IconVertexColor"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _OverrideColor ("Override Color", Vector) = (0,0,0,0)
        _Ambient ("Ambient", Range(0,1)) = 0.62
        _KeyDir ("Key Direction", Vector) = (0.35,0.55,-0.75,0)
        _KeyStrength ("Key Strength", Range(0,2)) = 0.85
        _FillDir ("Fill Direction", Vector) = (-0.65,-0.25,-0.55,0)
        _FillStrength ("Fill Strength", Range(0,2)) = 0.30
        _Flat ("Flat (no shading)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 nrm : TEXCOORD0; };
            fixed4 _Tint; float4 _OverrideColor; float _Ambient; float4 _KeyDir; float _KeyStrength; float4 _FillDir; float _FillStrength; float _Flat;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = (_OverrideColor.a > 0.0) ? _OverrideColor : v.color;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nrm);
                float hl = dot(n, normalize(_KeyDir.xyz)) * 0.5 + 0.5; hl *= hl;
                float hf = dot(n, normalize(_FillDir.xyz)) * 0.5 + 0.5;
                float lit = _Ambient + hl * _KeyStrength + hf * hf * _FillStrength;
                lit = lerp(lit, 1.0, _Flat);
                return fixed4(saturate(i.color.rgb * _Tint.rgb * lit), 1.0);
            }
            ENDHLSL
        }
    }
}
