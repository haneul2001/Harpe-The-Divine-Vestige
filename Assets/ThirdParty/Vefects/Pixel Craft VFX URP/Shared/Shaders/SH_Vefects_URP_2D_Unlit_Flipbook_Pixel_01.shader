// Made with Amplify Shader Editor v1.9.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_URP_2D_Unlit_Flipbook_Pixel_01"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[Space(13)][Header(Main Texture)][Space(13)] _MainTexture( "Main Texture", 2D ) = "white" {}
		[HDR] _R( "R", Color ) = ( 1, 0.9719134, 0.5896226, 0 )
		[HDR] _G( "G", Color ) = ( 1, 0.7230805, 0.25, 0 )
		[HDR] _B( "B", Color ) = ( 0.5943396, 0.259371, 0.09812209, 0 )
		[HDR] _Outline( "Outline", Color ) = ( 0.2169811, 0.03320287, 0.02354041, 0 )
		_FlatColor( "Flat Color", Range( 0, 1 ) ) = 0
		_Emissive( "Emissive", Float ) = 1
		[Space(13)][Header(AR)][Space(13)] _Cull( "Cull", Float ) = 2
		_Src( "Src", Float ) = 5
		_Dst( "Dst", Float ) = 10
		_ZWrite( "ZWrite", Float ) = 0
		_ZTest( "ZTest", Float ) = 2


		[HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Lit" "ShaderGraphShader"="true" }

		Cull [_Cull]

		HLSLINCLUDE
		#pragma target 2.0
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		ENDHLSL

		
		Pass
		{
			
			Name "Sprite Lit"
            Tags { "LightMode"="Universal2D" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_VERSION 19908
			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define VARYINGS_NEED_SCREENPOSITION
            #define FEATURES_GRAPH_VERTEX

			#define SHADERPASS SHADERPASS_SPRITELIT

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			// #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0


			half4 _RendererColor;

			sampler2D _MainTexture;
			CBUFFER_START( UnityPerMaterial )
			float4 _Outline;
			float4 _B;
			float4 _G;
			float4 _R;
			float _Src;
			float _Dst;
			float _ZTest;
			float _ZWrite;
			float _Cull;
			float _FlatColor;
			float _Emissive;
			CBUFFER_END


			struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
				float _EnableAlphaTexture;
			#endif

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			#ifdef UNITY_INSTANCING_ENABLED
				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );
			#endif

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.positionCS = vertexInput.positionCS;
				o.positionWS = vertexInput.positionWS;
				o.texCoord0 = v.uv0;
				o.color = v.color * _RendererColor;
			#ifdef UNITY_INSTANCING_ENABLED
				o.color *= unity_SpriteColor;
			#endif
				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float4 positionCS = IN.positionCS;
				float3 positionWS = IN.positionWS;

				float2 texCoord122 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 tex2DNode45 = tex2D( _MainTexture, texCoord122 );
				float4 lerpResult97 = lerp( _Outline , _B , tex2DNode45.b);
				float4 lerpResult112 = lerp( lerpResult97 , _G , tex2DNode45.g);
				float4 lerpResult111 = lerp( lerpResult112 , _R , tex2DNode45.r);
				float4 lerpResult88 = lerp( ( IN.color * lerpResult111 ) , IN.color , _FlatColor);
				float4 color125 = ( lerpResult88 * _Emissive );
				
				float mainTex_VC_alha52 = IN.color.a;
				float mainTex_alpha48 = tex2DNode45.a;
				float opacity61 = ( mainTex_VC_alha52 * mainTex_alpha48 );
				

				float3 BaseColor = color125.rgb;
				float Alpha = opacity61;
				float3 Normal = float3( 0, 0, 1 );
				float AlphaClipThreshold = 0.5;

				half4 Color = half4( BaseColor, Alpha );

			#if defined( ALPHA_CLIP_THRESHOLD )
				clip( Color.a - AlphaClipThreshold );
			#endif

			#if defined(DEBUG_DISPLAY)
				SurfaceData2D surfaceData;
				InitializeSurfaceData(Color.rgb, Color.a, surfaceData);
				InputData2D inputData;
				InitializeInputData(positionWS.xy, half2(IN.texCoord0.xy), inputData);
				half4 debugColor = 0;

				SETUP_DEBUG_DATA_2D(inputData, positionWS);

				if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
				{
					return debugColor;
				}
			#endif

			#if ETC1_EXTERNAL_ALPHA
				float4 alpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, IN.texCoord0.xy);
				Color.a = lerp( Color.a, alpha.r, _EnableAlphaTexture);
			#endif

				Color *= IN.color;

				return Color;
			}

			ENDHLSL
		}

		
		Pass
		{
			
            Name "Sprite Normal"
            Tags { "LightMode"="NormalsRendering" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZTest LEqual
			ZWrite Off
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_VERSION 19908
			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ SKINNED_SPRITE

			#define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_NORMAL_WS
            #define VARYINGS_NEED_TANGENT_WS
            #define FEATURES_GRAPH_VERTEX

			#define SHADERPASS SHADERPASS_SPRITENORMAL

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			// #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0


			sampler2D _MainTexture;
			CBUFFER_START( UnityPerMaterial )
			float4 _Outline;
			float4 _B;
			float4 _G;
			float4 _R;
			float _Src;
			float _Dst;
			float _ZTest;
			float _ZWrite;
			float _Cull;
			float _FlatColor;
			float _Emissive;
			CBUFFER_END


			struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				float4 tangentWS : TEXCOORD3;
				float3 bitangentWS : TEXCOORD4;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			#ifdef UNITY_INSTANCING_ENABLED
				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );
			#endif

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.texCoord0 = v.uv0;
				o.color = v.color;
			#ifdef UNITY_INSTANCING_ENABLED
				o.color *= unity_SpriteColor;
			#endif
				o.positionCS = vertexInput.positionCS;

				float3 normalWS = TransformObjectToWorldNormal(v.normal);
				o.normalWS = -GetViewForwardDir();
				float4 tangentWS = float4( TransformObjectToWorldDir(v.tangent.xyz), v.tangent.w);
				o.tangentWS = normalize(tangentWS);
				half crossSign = (tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
				o.bitangentWS = crossSign * cross(normalWS, tangentWS.xyz) * tangentWS.w;
				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float mainTex_VC_alha52 = IN.color.a;
				float2 texCoord122 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 tex2DNode45 = tex2D( _MainTexture, texCoord122 );
				float mainTex_alpha48 = tex2DNode45.a;
				float opacity61 = ( mainTex_VC_alha52 * mainTex_alpha48 );
				

				float Alpha = opacity61;
				float3 Normal = float3( 0, 0, 1 );
				float AlphaClipThreshold = 0.5;

				half4 Color = half4(1.0,1.0,1.0, Alpha);

				#if defined( ALPHA_CLIP_THRESHOLD )
					clip( Color.a - AlphaClipThreshold );
				#endif

				return NormalsRenderingShared(Color, Normal, IN.tangentWS.xyz, IN.bitangentWS, IN.normalWS);
			}

			ENDHLSL
		}

		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }

            Cull Off

            HLSLPROGRAM

			#define ASE_VERSION 19908
			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX

            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENESELECTIONPASS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			// #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_TEXTURE_COORDINATES0


			sampler2D _MainTexture;
			CBUFFER_START( UnityPerMaterial )
			float4 _Outline;
			float4 _B;
			float4 _G;
			float4 _R;
			float _Src;
			float _Dst;
			float _ZTest;
			float _ZWrite;
			float _Cull;
			float _FlatColor;
			float _Emissive;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            int _ObjectId;
            int _PassValue;

			
			VertexOutput vert(VertexInput v )
			{
				VertexOutput o = (VertexOutput)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			#ifdef UNITY_INSTANCING_ENABLED
				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );
			#endif

				o.ase_color = v.ase_color;
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.positionCS = vertexInput.positionCS;
				return o;
			}

			half4 frag(VertexOutput IN) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float mainTex_VC_alha52 = IN.ase_color.a;
				float2 texCoord122 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 tex2DNode45 = tex2D( _MainTexture, texCoord122 );
				float mainTex_alpha48 = tex2DNode45.a;
				float opacity61 = ( mainTex_VC_alha52 * mainTex_alpha48 );
				

				float Alpha = opacity61;
				float AlphaClipThreshold = 0.5;

				#if defined( ALPHA_CLIP_THRESHOLD )
					clip( Alpha - AlphaClipThreshold );
				#endif

				return half4(_ObjectId, _PassValue, 1.0, 1.0);
			}

            ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

			Cull Off

            HLSLPROGRAM

			#define ASE_VERSION 19908
			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX

            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENEPICKINGPASS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			// #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        	#define ASE_NEEDS_TEXTURE_COORDINATES0


			sampler2D _MainTexture;
			CBUFFER_START( UnityPerMaterial )
			float4 _Outline;
			float4 _B;
			float4 _G;
			float4 _R;
			float _Src;
			float _Dst;
			float _ZTest;
			float _ZWrite;
			float _Cull;
			float _FlatColor;
			float _Emissive;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            float4 _SelectionID;

			
			VertexOutput vert(VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			#ifdef UNITY_INSTANCING_ENABLED
				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );
			#endif

				o.ase_color = v.ase_color;
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.positionCS = vertexInput.positionCS;
				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float mainTex_VC_alha52 = IN.ase_color.a;
				float2 texCoord122 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float4 tex2DNode45 = tex2D( _MainTexture, texCoord122 );
				float mainTex_alpha48 = tex2DNode45.a;
				float opacity61 = ( mainTex_VC_alha52 * mainTex_alpha48 );
				

				float Alpha = opacity61;
				float AlphaClipThreshold = 0.5;

				#if defined( ALPHA_CLIP_THRESHOLD )
					clip( Alpha - AlphaClipThreshold );
				#endif

				return unity_SelectionID;
			}

            ENDHLSL
        }

		
		Pass
		{
			
            Name "Sprite Forward"
            Tags { "LightMode"="UniversalForward" }

			Blend [_Src] [_Dst], One OneMinusSrcAlpha
			ZTest [_ZTest]
			ZWrite [_ZTest]
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#define ASE_VERSION 19908
			#define ASE_SRP_VERSION 140007


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX

			#define SHADERPASS SHADERPASS_SPRITEFORWARD

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			// #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0


			half4 _RendererColor;

			sampler2D _MainTexture;
			CBUFFER_START( UnityPerMaterial )
			float4 _Outline;
			float4 _B;
			float4 _G;
			float4 _R;
			float _Src;
			float _Dst;
			float _ZTest;
			float _ZWrite;
			float _Cull;
			float _FlatColor;
			float _Emissive;
			CBUFFER_END


			struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if ETC1_EXTERNAL_ALPHA
				TEXTURE2D( _AlphaTex ); SAMPLER( sampler_AlphaTex );
				float _EnableAlphaTexture;
			#endif

			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

			#ifdef UNITY_INSTANCING_ENABLED
				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );
			#endif

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.positionCS = vertexInput.positionCS;
				o.positionWS = vertexInput.positionWS;
				o.texCoord0 = v.uv0;
				o.color = v.color * _RendererColor;
			#ifdef UNITY_INSTANCING_ENABLED
				o.color *= unity_SpriteColor;
			#endif
				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float4 positionCS = IN.positionCS;
				float3 positionWS = IN.positionWS;

				float2 texCoord122 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float4 tex2DNode45 = tex2D( _MainTexture, texCoord122 );
				float4 lerpResult97 = lerp( _Outline , _B , tex2DNode45.b);
				float4 lerpResult112 = lerp( lerpResult97 , _G , tex2DNode45.g);
				float4 lerpResult111 = lerp( lerpResult112 , _R , tex2DNode45.r);
				float4 lerpResult88 = lerp( ( IN.color * lerpResult111 ) , IN.color , _FlatColor);
				float4 color125 = ( lerpResult88 * _Emissive );
				
				float mainTex_VC_alha52 = IN.color.a;
				float mainTex_alpha48 = tex2DNode45.a;
				float opacity61 = ( mainTex_VC_alha52 * mainTex_alpha48 );
				

				float3 BaseColor = color125.rgb;
				float Alpha = opacity61;
				float3 Normal = float3( 0, 0, 1 );
				float AlphaClipThreshold = 0.5;

				half4 Color = half4( BaseColor,  Alpha);

			#if defined( ALPHA_CLIP_THRESHOLD )
				clip( Color.a - AlphaClipThreshold );
			#endif

			#if defined(DEBUG_DISPLAY)
				SurfaceData2D surfaceData;
				InitializeSurfaceData(Color.rgb, Color.a, surfaceData);
				InputData2D inputData;
				InitializeInputData(positionWS.xy, half2(IN.texCoord0.xy), inputData);
				half4 debugColor = 0;

				SETUP_DEBUG_DATA_2D(inputData, positionWS);

				if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
				{
					return debugColor;
				}
			#endif

			#if ETC1_EXTERNAL_ALPHA
				float4 alpha = SAMPLE_TEXTURE2D( _AlphaTex, sampler_AlphaTex, IN.texCoord0.xy );
				Color.a = lerp( Color.a, alpha.r, _EnableAlphaTexture );
			#endif

				Color *= IN.color;

				return Color;
			}

			ENDHLSL
		}
		
	}
	CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19908
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;36;-3072,-1792;Inherit;False;2564.097;1411.33;Color;18;124;99;106;88;105;112;111;101;97;96;93;92;52;48;47;45;122;125;;0,0,0,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;122;-2944,-640;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;45;-2432,-640;Inherit;True;Property;_MainTexture;Main Texture;0;0;Create;True;0;0;0;False;3;Space(13);Header(Main Texture);Space(13);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;92;-2960,-1216;Inherit;False;Property;_B;B;3;1;[HDR];Create;True;0;0;0;False;0;False;0.5943396,0.259371,0.09812209,0;0.2641509,0.2616589,0.2554289,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;93;-2960,-960;Inherit;False;Property;_Outline;Outline;4;1;[HDR];Create;True;0;0;0;False;0;False;0.2169811,0.03320287,0.02354041,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;96;-2960,-1472;Inherit;False;Property;_G;G;2;1;[HDR];Create;True;0;0;0;False;0;False;1,0.7230805,0.25,0;1,0.3523919,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;97;-2448,-1344;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;-2960,-1728;Inherit;False;Property;_R;R;1;1;[HDR];Create;True;0;0;0;False;0;False;1,0.9719134,0.5896226,0;0.3679245,0.3679245,0.3679245,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;112;-2064,-1472;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;-1792,-1152;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;111;-1808,-1728;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;21;-2560,320;Inherit;False;1154.111;382.0739;Opacity;4;61;123;53;57;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;52;-1792,-896;Inherit;False;mainTex_VC_alha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;48;-2048,-640;Inherit;False;mainTex_alpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;105;-1424,-1728;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;106;-1408,-896;Inherit;False;Property;_FlatColor;Flat Color;5;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;57;-2432,448;Inherit;False;52;mainTex_VC_alha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;53;-2432,576;Inherit;False;48;mainTex_alpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;88;-1408,-1152;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;124;-1024,-1024;Inherit;False;Property;_Emissive;Emissive;6;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;123;-2048,448;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;99;-1024,-1152;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;336,-48;Inherit;False;1243;166;AR;5;110;80;78;82;83;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;-1792,448;Inherit;False;opacity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;125;-768,-1152;Inherit;False;color;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;78;640,0;Inherit;False;Property;_Src;Src;8;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;110;896,0;Inherit;False;Property;_Dst;Dst;9;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;82;1408,0;Inherit;False;Property;_ZTest;ZTest;11;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;83;1152,0;Inherit;False;Property;_ZWrite;ZWrite;10;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;80;384,0;Inherit;False;Property;_Cull;Cull;7;0;Create;True;0;0;0;True;3;Space(13);Header(AR);Space(13);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;73;-384,128;Inherit;False;61;opacity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;126;-384,0;Inherit;False;125;color;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;131;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;Sprite Lit;0;0;Sprite Lit;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;False;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;132;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;Sprite Normal;0;1;Sprite Normal;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;False;True;1;LightMode=NormalsRendering;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;133;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;SceneSelectionPass;0;2;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;134;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;ScenePickingPass;0;3;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;135;0,0;Float;False;True;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;16;Vefects/SH_Vefects_URP_2D_Unlit_Flipbook_Pixel_01;ece0159bad6633944bf6b818f4dd296c;True;Sprite Forward;0;4;Sprite Forward;7;True;False;False;False;False;False;False;False;False;False;False;False;False;True;True;2;True;_Cull;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;True;True;2;0;True;_Src;0;True;_Dst;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;2;True;_ZTest;True;3;True;_ZTest;True;True;0;False;;0;False;;False;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;4;Alpha Clipping;0;0;Vertex Position;1;0;Debug Display;0;0;External Alpha;0;0;0;5;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;113;336,-224;Inherit;False;304;100;Ge Lush was here! <3;0;Ge Lush was here! <3;0,0,0,1;0;0
WireConnection;45;1;122;0
WireConnection;97;0;93;0
WireConnection;97;1;92;0
WireConnection;97;2;45;3
WireConnection;112;0;97;0
WireConnection;112;1;96;0
WireConnection;112;2;45;2
WireConnection;111;0;112;0
WireConnection;111;1;101;0
WireConnection;111;2;45;1
WireConnection;52;0;47;4
WireConnection;48;0;45;4
WireConnection;105;0;47;0
WireConnection;105;1;111;0
WireConnection;88;0;105;0
WireConnection;88;1;47;0
WireConnection;88;2;106;0
WireConnection;123;0;57;0
WireConnection;123;1;53;0
WireConnection;99;0;88;0
WireConnection;99;1;124;0
WireConnection;61;0;123;0
WireConnection;125;0;99;0
WireConnection;135;0;126;0
WireConnection;135;1;73;0
ASEEND*/
//CHKSM=BC8863FF1CF4BDA1A41D282BA856A00680193277