Shader "Necromancer/Village Surface"
{
    Properties
    {
        _MainTex ("Surface atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cell ("Atlas cell", Vector) = (0, 0, 0, 0)
        _TileMeters ("World tile size", Float) = 0
        [HideInInspector] _Road0 ("Road 1", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road1 ("Road 2", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road2 ("Road 3", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road3 ("Road 4", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road4 ("Road 5", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road5 ("Yard", Vector) = (10000,10000,0,0)
        [HideInInspector] _Road6 ("Farm track", Vector) = (10000,10000,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface Surface Lambert
        #pragma shader_feature_local _ VILLAGE_TERRAIN
        sampler2D _MainTex;
        fixed4 _Color;
        float4 _Cell;
        float _TileMeters;
        float4 _Road0, _Road1, _Road2, _Road3, _Road4, _Road5, _Road6;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        fixed3 Tile(float2 cell, float2 uv)
        {
            // Leave a small margin so atlas cells cannot bleed into one another.
            return tex2D(_MainTex, (cell + lerp(.016, .984, uv)) / float2(3, 2)).rgb;
        }

        float RoadDistance(float2 position, float4 road)
        {
            float2 outside = abs(position - road.xy) - road.zw;
            return max(outside.x, outside.y);
        }

        void Surface(Input input, inout SurfaceOutput output)
        {
            float2 world = input.worldPos.xz;
            float2 tile = _TileMeters > 0 ? frac(world / _TileMeters) : saturate(input.uv_MainTex);
            fixed3 color = Tile(_Cell.xy, tile);
            #if defined(VILLAGE_TERRAIN)
                float road = min(min(RoadDistance(world, _Road0), RoadDistance(world, _Road1)),
                    min(RoadDistance(world, _Road2), RoadDistance(world, _Road3)));
                road = min(road, min(RoadDistance(world, _Road4),
                    min(RoadDistance(world, _Road5), RoadDistance(world, _Road6))));
                // The packed-earth shoulder breaks up in small square clusters at the edge.
                float grit = frac(sin(dot(floor(world * 11), float2(12.9898, 78.233))) * 43758.5453);
                float blend = 1 - smoothstep(-.32, .17, road + (grit - .5) * .28);
                color = lerp(color, Tile(float2(1, 1), frac(world / 4.5)), blend);
            #endif
            output.Albedo = color * _Color.rgb;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
