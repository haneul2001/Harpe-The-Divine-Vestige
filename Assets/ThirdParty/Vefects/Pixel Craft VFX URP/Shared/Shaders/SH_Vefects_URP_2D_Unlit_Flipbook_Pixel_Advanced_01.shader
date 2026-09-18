// Made with Amplify Shader Editor v1.9.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_URP_2D_Unlit_Flipbook_Pixel_Advanced_01"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		_Emissive( "Emissive", Float ) = 1
		_FlipbookX( "Flipbook X", Float ) = 7
		_FlipbookY( "Flipbook Y", Float ) = 1
		[Space(13)][Header(Main Texture)][Space(13)] _MainTexture( "Main Texture", 2D ) = "white" {}
		[HDR] _R( "R", Color ) = ( 1, 0.9719134, 0.5896226, 0 )
		[HDR] _G( "G", Color ) = ( 1, 0.7230805, 0.25, 0 )
		[HDR] _B( "B", Color ) = ( 0.5943396, 0.259371, 0.09812209, 0 )
		[HDR] _Outline( "Outline", Color ) = ( 0.2169811, 0.03320287, 0.02354041, 0 )
		_FlatColor( "Flat Color", Range( 0, 1 ) ) = 0
		_UVS( "UV S", Vector ) = ( 1, 1, 0, 0 )
		_UVP( "UV P", Vector ) = ( 0, 0, 0, 0 )
		[Space(13)][Header(DisolveMapping)][Space(13)] _disolveMap( "disolveMap", 2D ) = "white" {}
		_DissolveMapScale( "Dissolve Map Scale", Float ) = 1
		[Space(13)][Header(Distortion)][Space(13)] _DistortionTexture( "Distortion Texture", 2D ) = "white" {}
		_DistortionLerp( "Distortion Lerp", Range( 0, 0.1 ) ) = 0
		_UVDS( "UV D S", Vector ) = ( 1, 1, 0, 0 )
		_UVDP( "UV D P", Vector ) = ( 0.1, -0.2, 0, 0 )
		[Space(33)][Header(Pixelate)][Space(13)][Toggle( _PIXELATE_ON )] _Pixelate( "Pixelate", Float ) = 0
		_PixelsMultiplier( "Pixels Multiplier", Float ) = 1
		_PixelsX( "Pixels X", Float ) = 32
		_PixelsY( "Pixels Y", Float ) = 32
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
			#pragma shader_feature_local _PIXELATE_ON


			half4 _RendererColor;

			sampler2D _MainTexture;
			sampler2D _DistortionTexture;
			sampler2D _disolveMap;
			CBUFFER_START( UnityPerMaterial )
			float4 _R;
			float4 _Outline;
			float4 _B;
			float4 _G;
			float2 _UVDS;
			float2 _UVP;
			float2 _UVS;
			float2 _UVDP;
			float _FlipbookX;
			float _Emissive;
			float _FlatColor;
			float _PixelsY;
			float _PixelsMultiplier;
			float _Src;
			float _DistortionLerp;
			float _FlipbookY;
			float _Cull;
			float _ZWrite;
			float _ZTest;
			float _Dst;
			float _PixelsX;
			float _DissolveMapScale;
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

				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner140 = ( 1.0 * _Time.y * _UVP + ( texCoord131 * _UVS ));
				float2 texCoord123 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner127 = ( 1.0 * _Time.y * _UVDP + ( texCoord123 * _UVDS ));
				float2 lerpResult139 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionTexture, panner127 )).rg + -0.5 ) * 2.0 ) , _DistortionLerp);
				float2 DistortionRegister149 = ( panner140 + lerpResult139 );
				half2 pixelateduv115 = floor( DistortionRegister149 * float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) ) + float2( 0,0 ) ) / float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) );
				#ifdef _PIXELATE_ON
				float2 staticSwitch118 = pixelateduv115;
				#else
				float2 staticSwitch118 = DistortionRegister149;
				#endif
				float4 tex2DNode45 = tex2D( _MainTexture, staticSwitch118 );
				float4 lerpResult97 = lerp( _Outline , _B , tex2DNode45.b);
				float4 lerpResult112 = lerp( lerpResult97 , _G , tex2DNode45.g);
				float4 lerpResult111 = lerp( lerpResult112 , _R , tex2DNode45.r);
				float4 lerpResult88 = lerp( ( IN.color * lerpResult111 ) , IN.color , _FlatColor);
				float4 color183 = ( lerpResult88 * _Emissive );
				
				float mainTex_alpha48 = tex2DNode45.a;
				float mainTex_VC_alha52 = IN.color.a;
				float Opacity_VTC_W158 = IN.texCoord0.z;
				float Opacity_VTC_T152 = IN.texCoord0.w;
				float temp_output_167_0 =  (( Opacity_VTC_T152 - 1.0 ) + ( Opacity_VTC_W158 - 0.0 ) * ( 1.0 - ( Opacity_VTC_T152 - 1.0 ) ) / ( 1.0 - 0.0 ) );
				float2 texCoord150 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float flipbook_X143 = _FlipbookX;
				float flipbook_Y144 = _FlipbookY;
				float2 appendResult151 = (float2(flipbook_X143 , flipbook_Y144));
				float smoothstepResult172 = smoothstep( temp_output_167_0 , ( temp_output_167_0 + Opacity_VTC_T152 ) , tex2D( _disolveMap, ( ( ( texCoord150 * appendResult151 ) * _DissolveMapScale ) + flipbook_X143 ) ).g);
				float disolveMapping173 = smoothstepResult172;
				float OpacityRegister179 = ( ( mainTex_alpha48 * mainTex_VC_alha52 ) * disolveMapping173 );
				

				float3 BaseColor = color183.rgb;
				float Alpha = OpacityRegister179;
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

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature_local _PIXELATE_ON


			sampler2D _MainTexture;
			sampler2D _DistortionTexture;
			sampler2D _disolveMap;
			CBUFFER_START( UnityPerMaterial )
			float4 _R;
			float4 _Outline;
			float4 _B;
			float4 _G;
			float2 _UVDS;
			float2 _UVP;
			float2 _UVS;
			float2 _UVDP;
			float _FlipbookX;
			float _Emissive;
			float _FlatColor;
			float _PixelsY;
			float _PixelsMultiplier;
			float _Src;
			float _DistortionLerp;
			float _FlipbookY;
			float _Cull;
			float _ZWrite;
			float _ZTest;
			float _Dst;
			float _PixelsX;
			float _DissolveMapScale;
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

				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner140 = ( 1.0 * _Time.y * _UVP + ( texCoord131 * _UVS ));
				float2 texCoord123 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner127 = ( 1.0 * _Time.y * _UVDP + ( texCoord123 * _UVDS ));
				float2 lerpResult139 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionTexture, panner127 )).rg + -0.5 ) * 2.0 ) , _DistortionLerp);
				float2 DistortionRegister149 = ( panner140 + lerpResult139 );
				half2 pixelateduv115 = floor( DistortionRegister149 * float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) ) + float2( 0,0 ) ) / float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) );
				#ifdef _PIXELATE_ON
				float2 staticSwitch118 = pixelateduv115;
				#else
				float2 staticSwitch118 = DistortionRegister149;
				#endif
				float4 tex2DNode45 = tex2D( _MainTexture, staticSwitch118 );
				float mainTex_alpha48 = tex2DNode45.a;
				float mainTex_VC_alha52 = IN.color.a;
				float Opacity_VTC_W158 = IN.texCoord0.z;
				float Opacity_VTC_T152 = IN.texCoord0.w;
				float temp_output_167_0 =  (( Opacity_VTC_T152 - 1.0 ) + ( Opacity_VTC_W158 - 0.0 ) * ( 1.0 - ( Opacity_VTC_T152 - 1.0 ) ) / ( 1.0 - 0.0 ) );
				float2 texCoord150 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float flipbook_X143 = _FlipbookX;
				float flipbook_Y144 = _FlipbookY;
				float2 appendResult151 = (float2(flipbook_X143 , flipbook_Y144));
				float smoothstepResult172 = smoothstep( temp_output_167_0 , ( temp_output_167_0 + Opacity_VTC_T152 ) , tex2D( _disolveMap, ( ( ( texCoord150 * appendResult151 ) * _DissolveMapScale ) + flipbook_X143 ) ).g);
				float disolveMapping173 = smoothstepResult172;
				float OpacityRegister179 = ( ( mainTex_alpha48 * mainTex_VC_alha52 ) * disolveMapping173 );
				

				float Alpha = OpacityRegister179;
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
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#pragma shader_feature_local _PIXELATE_ON


			sampler2D _MainTexture;
			sampler2D _DistortionTexture;
			sampler2D _disolveMap;
			CBUFFER_START( UnityPerMaterial )
			float4 _R;
			float4 _Outline;
			float4 _B;
			float4 _G;
			float2 _UVDS;
			float2 _UVP;
			float2 _UVS;
			float2 _UVDP;
			float _FlipbookX;
			float _Emissive;
			float _FlatColor;
			float _PixelsY;
			float _PixelsMultiplier;
			float _Src;
			float _DistortionLerp;
			float _FlipbookY;
			float _Cull;
			float _ZWrite;
			float _ZTest;
			float _Dst;
			float _PixelsX;
			float _DissolveMapScale;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
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

				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;
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

				float2 texCoord131 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner140 = ( 1.0 * _Time.y * _UVP + ( texCoord131 * _UVS ));
				float2 texCoord123 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner127 = ( 1.0 * _Time.y * _UVDP + ( texCoord123 * _UVDS ));
				float2 lerpResult139 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionTexture, panner127 )).rg + -0.5 ) * 2.0 ) , _DistortionLerp);
				float2 DistortionRegister149 = ( panner140 + lerpResult139 );
				half2 pixelateduv115 = floor( DistortionRegister149 * float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) ) + float2( 0,0 ) ) / float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) );
				#ifdef _PIXELATE_ON
				float2 staticSwitch118 = pixelateduv115;
				#else
				float2 staticSwitch118 = DistortionRegister149;
				#endif
				float4 tex2DNode45 = tex2D( _MainTexture, staticSwitch118 );
				float mainTex_alpha48 = tex2DNode45.a;
				float mainTex_VC_alha52 = IN.ase_color.a;
				float Opacity_VTC_W158 = IN.ase_texcoord.z;
				float Opacity_VTC_T152 = IN.ase_texcoord.w;
				float temp_output_167_0 =  (( Opacity_VTC_T152 - 1.0 ) + ( Opacity_VTC_W158 - 0.0 ) * ( 1.0 - ( Opacity_VTC_T152 - 1.0 ) ) / ( 1.0 - 0.0 ) );
				float2 texCoord150 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float flipbook_X143 = _FlipbookX;
				float flipbook_Y144 = _FlipbookY;
				float2 appendResult151 = (float2(flipbook_X143 , flipbook_Y144));
				float smoothstepResult172 = smoothstep( temp_output_167_0 , ( temp_output_167_0 + Opacity_VTC_T152 ) , tex2D( _disolveMap, ( ( ( texCoord150 * appendResult151 ) * _DissolveMapScale ) + flipbook_X143 ) ).g);
				float disolveMapping173 = smoothstepResult172;
				float OpacityRegister179 = ( ( mainTex_alpha48 * mainTex_VC_alha52 ) * disolveMapping173 );
				

				float Alpha = OpacityRegister179;
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
        	#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
        	#pragma shader_feature_local _PIXELATE_ON


			sampler2D _MainTexture;
			sampler2D _DistortionTexture;
			sampler2D _disolveMap;
			CBUFFER_START( UnityPerMaterial )
			float4 _R;
			float4 _Outline;
			float4 _B;
			float4 _G;
			float2 _UVDS;
			float2 _UVP;
			float2 _UVS;
			float2 _UVDP;
			float _FlipbookX;
			float _Emissive;
			float _FlatColor;
			float _PixelsY;
			float _PixelsMultiplier;
			float _Src;
			float _DistortionLerp;
			float _FlipbookY;
			float _Cull;
			float _ZWrite;
			float _ZTest;
			float _Dst;
			float _PixelsX;
			float _DissolveMapScale;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
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

				o.ase_texcoord = v.ase_texcoord;
				o.ase_color = v.ase_color;

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

				float2 texCoord131 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner140 = ( 1.0 * _Time.y * _UVP + ( texCoord131 * _UVS ));
				float2 texCoord123 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner127 = ( 1.0 * _Time.y * _UVDP + ( texCoord123 * _UVDS ));
				float2 lerpResult139 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionTexture, panner127 )).rg + -0.5 ) * 2.0 ) , _DistortionLerp);
				float2 DistortionRegister149 = ( panner140 + lerpResult139 );
				half2 pixelateduv115 = floor( DistortionRegister149 * float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) ) + float2( 0,0 ) ) / float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) );
				#ifdef _PIXELATE_ON
				float2 staticSwitch118 = pixelateduv115;
				#else
				float2 staticSwitch118 = DistortionRegister149;
				#endif
				float4 tex2DNode45 = tex2D( _MainTexture, staticSwitch118 );
				float mainTex_alpha48 = tex2DNode45.a;
				float mainTex_VC_alha52 = IN.ase_color.a;
				float Opacity_VTC_W158 = IN.ase_texcoord.z;
				float Opacity_VTC_T152 = IN.ase_texcoord.w;
				float temp_output_167_0 =  (( Opacity_VTC_T152 - 1.0 ) + ( Opacity_VTC_W158 - 0.0 ) * ( 1.0 - ( Opacity_VTC_T152 - 1.0 ) ) / ( 1.0 - 0.0 ) );
				float2 texCoord150 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float flipbook_X143 = _FlipbookX;
				float flipbook_Y144 = _FlipbookY;
				float2 appendResult151 = (float2(flipbook_X143 , flipbook_Y144));
				float smoothstepResult172 = smoothstep( temp_output_167_0 , ( temp_output_167_0 + Opacity_VTC_T152 ) , tex2D( _disolveMap, ( ( ( texCoord150 * appendResult151 ) * _DissolveMapScale ) + flipbook_X143 ) ).g);
				float disolveMapping173 = smoothstepResult172;
				float OpacityRegister179 = ( ( mainTex_alpha48 * mainTex_VC_alha52 ) * disolveMapping173 );
				

				float Alpha = OpacityRegister179;
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
			ZWrite [_ZWrite]
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
			#pragma shader_feature_local _PIXELATE_ON


			half4 _RendererColor;

			sampler2D _MainTexture;
			sampler2D _DistortionTexture;
			sampler2D _disolveMap;
			CBUFFER_START( UnityPerMaterial )
			float4 _R;
			float4 _Outline;
			float4 _B;
			float4 _G;
			float2 _UVDS;
			float2 _UVP;
			float2 _UVS;
			float2 _UVDP;
			float _FlipbookX;
			float _Emissive;
			float _FlatColor;
			float _PixelsY;
			float _PixelsMultiplier;
			float _Src;
			float _DistortionLerp;
			float _FlipbookY;
			float _Cull;
			float _ZWrite;
			float _ZTest;
			float _Dst;
			float _PixelsX;
			float _DissolveMapScale;
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

				float2 texCoord131 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner140 = ( 1.0 * _Time.y * _UVP + ( texCoord131 * _UVS ));
				float2 texCoord123 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner127 = ( 1.0 * _Time.y * _UVDP + ( texCoord123 * _UVDS ));
				float2 lerpResult139 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionTexture, panner127 )).rg + -0.5 ) * 2.0 ) , _DistortionLerp);
				float2 DistortionRegister149 = ( panner140 + lerpResult139 );
				half2 pixelateduv115 = floor( DistortionRegister149 * float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) ) + float2( 0,0 ) ) / float2( ( _PixelsX * _PixelsMultiplier ), ( _PixelsY * _PixelsMultiplier ) );
				#ifdef _PIXELATE_ON
				float2 staticSwitch118 = pixelateduv115;
				#else
				float2 staticSwitch118 = DistortionRegister149;
				#endif
				float4 tex2DNode45 = tex2D( _MainTexture, staticSwitch118 );
				float4 lerpResult97 = lerp( _Outline , _B , tex2DNode45.b);
				float4 lerpResult112 = lerp( lerpResult97 , _G , tex2DNode45.g);
				float4 lerpResult111 = lerp( lerpResult112 , _R , tex2DNode45.r);
				float4 lerpResult88 = lerp( ( IN.color * lerpResult111 ) , IN.color , _FlatColor);
				float4 color183 = ( lerpResult88 * _Emissive );
				
				float mainTex_alpha48 = tex2DNode45.a;
				float mainTex_VC_alha52 = IN.color.a;
				float Opacity_VTC_W158 = IN.texCoord0.z;
				float Opacity_VTC_T152 = IN.texCoord0.w;
				float temp_output_167_0 =  (( Opacity_VTC_T152 - 1.0 ) + ( Opacity_VTC_W158 - 0.0 ) * ( 1.0 - ( Opacity_VTC_T152 - 1.0 ) ) / ( 1.0 - 0.0 ) );
				float2 texCoord150 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float flipbook_X143 = _FlipbookX;
				float flipbook_Y144 = _FlipbookY;
				float2 appendResult151 = (float2(flipbook_X143 , flipbook_Y144));
				float smoothstepResult172 = smoothstep( temp_output_167_0 , ( temp_output_167_0 + Opacity_VTC_T152 ) , tex2D( _disolveMap, ( ( ( texCoord150 * appendResult151 ) * _DissolveMapScale ) + flipbook_X143 ) ).g);
				float disolveMapping173 = smoothstepResult172;
				float OpacityRegister179 = ( ( mainTex_alpha48 * mainTex_VC_alha52 ) * disolveMapping173 );
				

				float3 BaseColor = color183.rgb;
				float Alpha = OpacityRegister179;
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
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;122;-4736,-1664;Inherit;False;1992;995;Distortion;18;149;145;140;139;136;135;134;133;132;131;130;129;128;127;126;125;124;123;;0,0,0,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;123;-4672,-976;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;124;-4416,-848;Inherit;False;Property;_UVDS;UV D S;15;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;125;-4416,-976;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;126;-4160,-848;Inherit;False;Property;_UVDP;UV D P;16;0;Create;True;0;0;0;False;0;False;0.1,-0.2;0.1,-0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.PannerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;127;-4160,-976;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;128;-3904,-976;Inherit;True;Property;_DistortionTexture;Distortion Texture;13;0;Create;True;0;0;0;False;3;Space(13);Header(Distortion);Space(13);False;-1;98c3d568d9032a34eb5b038e20fea05d;98c3d568d9032a34eb5b038e20fea05d;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ComponentMaskNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;129;-3520,-976;Inherit;False;True;True;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;130;-3776,-1488;Inherit;False;Property;_UVS;UV S;9;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;131;-4032,-1616;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;132;-3520,-1488;Inherit;False;Property;_UVP;UV P;10;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;133;-3520,-1104;Inherit;False;Property;_DistortionLerp;Distortion Lerp;14;0;Create;True;0;0;0;False;0;False;0;0;0;0.1;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;134;-3520,-1232;Inherit;False;Constant;_Vector0;Vector 0;8;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;135;-3264,-976;Inherit;False;ConstantBiasScale;-1;;2;63208df05c83e8e49a48ffbdce2e43a0;0;3;3;FLOAT2;0,0;False;1;FLOAT;-0.5;False;2;FLOAT;2;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;136;-3776,-1616;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;139;-3136,-1232;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;140;-3520,-1616;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;137;-4608,1472;Inherit;False;Property;_FlipbookX;Flipbook X;1;0;Create;True;0;0;0;False;0;False;7;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;138;-4608,1600;Inherit;False;Property;_FlipbookY;Flipbook Y;2;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;36;-2416,-1728;Inherit;False;1896;1857.979;Color;22;48;45;112;111;106;105;101;97;96;93;92;88;52;47;38;115;116;117;118;119;120;121;;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;145;-3136,-1616;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;142;-4704,208;Inherit;False;1761.094;1140.744;DisolveMaping;22;173;172;171;170;169;168;167;166;165;164;163;162;161;160;157;156;155;154;151;150;147;146;;0.1037736,0.1037736,0.1037736,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;143;-4352,1472;Inherit;False;flipbook X;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;144;-4352,1600;Inherit;False;flipbook Y;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;117;-2304,-128;Inherit;False;Property;_PixelsY;Pixels Y;20;0;Create;True;0;0;0;False;0;False;32;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;116;-2304,-256;Inherit;False;Property;_PixelsX;Pixels X;19;0;Create;True;0;0;0;False;0;False;32;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;121;-2304,0;Inherit;False;Property;_PixelsMultiplier;Pixels Multiplier;18;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;149;-2976,-1616;Inherit;False;DistortionRegister;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;146;-4352,960;Inherit;False;143;flipbook X;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;147;-4352,1088;Inherit;False;144;flipbook Y;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;119;-2048,-256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;120;-2048,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;141;-4720,-256;Inherit;False;1768.791;450.8129;Opacity;11;179;178;177;176;175;174;159;158;153;152;148;;0,0,0,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;38;-2304,-640;Inherit;False;149;DistortionRegister;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;150;-4608,832;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;151;-4096,960;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TFHCPixelate, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;115;-2304,-384;Inherit;False;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;148;-4608,-192;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;152;-4224,-112;Inherit;False;Opacity_VTC_T;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;156;-4096,832;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;157;-3840,960;Inherit;False;Property;_DissolveMapScale;Dissolve Map Scale;12;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;118;-2048,-640;Inherit;False;Property;_Pixelate;Pixelate;17;0;Create;True;0;0;0;False;3;Space(33);Header(Pixelate);Space(13);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;154;-4656,560;Inherit;False;Constant;_Float2;Float 2;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;155;-4688,320;Inherit;False;152;Opacity_VTC_T;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;158;-4224,-192;Inherit;False;Opacity_VTC_W;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;165;-3840,832;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;166;-3584,960;Inherit;False;143;flipbook X;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;45;-1792,-640;Inherit;True;Property;_MainTexture;Main Texture;3;0;Create;True;0;0;0;False;3;Space(13);Header(Main Texture);Space(13);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;92;-2304,-1152;Inherit;False;Property;_B;B;6;1;[HDR];Create;True;0;0;0;False;0;False;0.5943396,0.259371,0.09812209,0;0.2641509,0.2616589,0.2554289,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;93;-2304,-896;Inherit;False;Property;_Outline;Outline;7;1;[HDR];Create;True;0;0;0;False;0;False;0.2169811,0.03320287,0.02354041,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;160;-4368,256;Inherit;False;158;Opacity_VTC_W;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;161;-4480,320;Inherit;False;Constant;_Float0;Float 0;20;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;162;-4480,432;Inherit;False;Constant;_Float1;Float 1;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;163;-4480,640;Inherit;False;Constant;_Float3;Float 3;20;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;164;-4480,528;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;168;-4144,256;Inherit;False;152;Opacity_VTC_T;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;169;-3584,832;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;96;-2304,-1408;Inherit;False;Property;_G;G;5;1;[HDR];Create;True;0;0;0;False;0;False;1,0.7230805,0.25,0;1,0.3523919,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;97;-1792,-1280;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;167;-4240,352;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;170;-3920,288;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;171;-3328,832;Inherit;True;Property;_disolveMap;disolveMap;11;0;Create;True;0;0;0;False;3;Space(13);Header(DisolveMapping);Space(13);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;-2304,-1664;Inherit;False;Property;_R;R;4;1;[HDR];Create;True;0;0;0;False;0;False;1,0.9719134,0.5896226,0;0.3679245,0.3679245,0.3679245,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;112;-1408,-1408;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;-1280,-1024;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;48;-1408,-640;Inherit;False;mainTex_alpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;52;-1280,-768;Inherit;False;mainTex_VC_alha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;172;-3776,400;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;111;-1152,-1664;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;173;-3584,400;Inherit;False;disolveMapping;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;174;-3840,-64;Inherit;False;48;mainTex_alpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;175;-3840,64;Inherit;False;52;mainTex_VC_alha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;105;-768,-1152;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;106;-1152,-384;Inherit;False;Property;_FlatColor;Flat Color;8;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;177;-3584,-64;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;176;-3456,64;Inherit;False;173;disolveMapping;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;88;-768,-640;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;184;-384,-512;Inherit;False;Property;_Emissive;Emissive;0;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;178;-3456,-64;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;180;-384,-640;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;336,-48;Inherit;False;1243;166;AR;5;110;80;78;82;83;;0,0,0,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;179;-3200,-64;Inherit;False;OpacityRegister;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;183;-128,-640;Inherit;False;color;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;78;640,0;Inherit;False;Property;_Src;Src;22;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;110;896,0;Inherit;False;Property;_Dst;Dst;23;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;82;1408,0;Inherit;False;Property;_ZTest;ZTest;25;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;83;1152,0;Inherit;False;Property;_ZWrite;ZWrite;24;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;80;384,0;Inherit;False;Property;_Cull;Cull;21;0;Create;True;0;0;0;True;3;Space(13);Header(AR);Space(13);False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;153;-4608,0;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;159;-4224,0;Inherit;False;Random Offset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;73;-384,128;Inherit;False;179;OpacityRegister;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;181;-384,0;Inherit;False;183;color;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;185;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;Sprite Lit;0;0;Sprite Lit;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;False;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;186;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;Sprite Normal;0;1;Sprite Normal;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;False;True;1;LightMode=NormalsRendering;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;187;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;SceneSelectionPass;0;2;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;188;0,0;Float;False;False;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;1;New Amplify Shader;ece0159bad6633944bf6b818f4dd296c;True;ScenePickingPass;0;3;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;189;0,0;Float;False;True;-1;2;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;16;Vefects/SH_Vefects_URP_2D_Unlit_Flipbook_Pixel_Advanced_01;ece0159bad6633944bf6b818f4dd296c;True;Sprite Forward;0;4;Sprite Forward;7;True;False;False;False;False;False;False;False;False;False;False;False;False;True;True;2;True;_Cull;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;ShaderGraphShader=true;True;0;True;12;all;0;True;True;1;0;True;_Src;0;True;_Dst;3;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;2;True;_ZWrite;True;3;True;_ZTest;True;True;0;False;;0;False;;False;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;4;Alpha Clipping;0;0;Vertex Position;1;0;Debug Display;0;0;External Alpha;0;0;0;5;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;113;336,-224;Inherit;False;304;100;Ge Lush was here! <3;0;Ge Lush was here! <3;0,0,0,1;0;0
WireConnection;125;0;123;0
WireConnection;125;1;124;0
WireConnection;127;0;125;0
WireConnection;127;2;126;0
WireConnection;128;1;127;0
WireConnection;129;0;128;0
WireConnection;135;3;129;0
WireConnection;136;0;131;0
WireConnection;136;1;130;0
WireConnection;139;0;134;0
WireConnection;139;1;135;0
WireConnection;139;2;133;0
WireConnection;140;0;136;0
WireConnection;140;2;132;0
WireConnection;145;0;140;0
WireConnection;145;1;139;0
WireConnection;143;0;137;0
WireConnection;144;0;138;0
WireConnection;149;0;145;0
WireConnection;119;0;116;0
WireConnection;119;1;121;0
WireConnection;120;0;117;0
WireConnection;120;1;121;0
WireConnection;151;0;146;0
WireConnection;151;1;147;0
WireConnection;115;0;38;0
WireConnection;115;1;119;0
WireConnection;115;2;120;0
WireConnection;152;0;148;4
WireConnection;156;0;150;0
WireConnection;156;1;151;0
WireConnection;118;1;38;0
WireConnection;118;0;115;0
WireConnection;158;0;148;3
WireConnection;165;0;156;0
WireConnection;165;1;157;0
WireConnection;45;1;118;0
WireConnection;164;0;155;0
WireConnection;164;1;154;0
WireConnection;169;0;165;0
WireConnection;169;1;166;0
WireConnection;97;0;93;0
WireConnection;97;1;92;0
WireConnection;97;2;45;3
WireConnection;167;0;160;0
WireConnection;167;1;161;0
WireConnection;167;2;162;0
WireConnection;167;3;164;0
WireConnection;167;4;163;0
WireConnection;170;0;167;0
WireConnection;170;1;168;0
WireConnection;171;1;169;0
WireConnection;112;0;97;0
WireConnection;112;1;96;0
WireConnection;112;2;45;2
WireConnection;48;0;45;4
WireConnection;52;0;47;4
WireConnection;172;0;171;2
WireConnection;172;1;167;0
WireConnection;172;2;170;0
WireConnection;111;0;112;0
WireConnection;111;1;101;0
WireConnection;111;2;45;1
WireConnection;173;0;172;0
WireConnection;105;0;47;0
WireConnection;105;1;111;0
WireConnection;177;0;174;0
WireConnection;177;1;175;0
WireConnection;88;0;105;0
WireConnection;88;1;47;0
WireConnection;88;2;106;0
WireConnection;178;0;177;0
WireConnection;178;1;176;0
WireConnection;180;0;88;0
WireConnection;180;1;184;0
WireConnection;179;0;178;0
WireConnection;183;0;180;0
WireConnection;159;0;153;1
WireConnection;189;0;181;0
WireConnection;189;1;73;0
ASEEND*/
//CHKSM=8F382E4482471C9EE1C2722E2E4ABB197D9D112F