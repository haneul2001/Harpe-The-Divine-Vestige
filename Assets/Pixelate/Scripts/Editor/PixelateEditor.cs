using System.Collections;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pixelate
{
    [CustomEditor(typeof(PixelateCaptureManager))]
    public class PixelateEditor : Editor
    {
        string targetName;

        Texture2D headerTexture;
        Texture2D pixelateLogo;
        //Texture2D previewImage;

        Rect headerSection;

        private PixelateCaptureManager helper;

        private const string ASSIGN_TARGET_INFO = "Assign a Target object you'd like to capture.";
        private const string ASSIGN_ANIM_INFO = "Assign at least one SourceClip to start previewing!";
        private const string ASSIGN_CAMERA_INFO = "Assign a camera to start capturing!";
        private const string EXPORT_LOCATION_WARNING = "Invalid export location! Must be inside this project.";

        private IEnumerator _currentCaptureRoutine;

        void OnEnable()
        {
            InitTextures();
        }

        void InitTextures()
        {
            if (EditorGUIUtility.isProSkin == true)
                pixelateLogo = Resources.Load<Texture2D>("Editor\\Pixelate Logo Light");
            else
                pixelateLogo = Resources.Load<Texture2D>("Editor\\Pixelate Logo Light");

            //previewImage = Resources.Load<Texture2D>("Editor\\preview");

            headerTexture = new Texture2D(1, 1);

            if (EditorGUIUtility.isProSkin == true)
                headerTexture.SetPixel(0, 0, new Color32(28, 28, 28, 255));
            else
                headerTexture.SetPixel(0, 0, new Color32(170, 170, 170, 255));

            headerTexture.Apply();
        }

        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(_currentCaptureRoutine != null))
            {
                InitiateBanner();

                helper = (PixelateCaptureManager)target;
                var targetProp = serializedObject.FindProperty("_target");
                var useAnimation = serializedObject.FindProperty("_useAnimation");
                var sourceClipsProp = serializedObject.FindProperty("_sourceClips");

                EditorGUILayout.PropertyField(targetProp);

                if (targetProp.objectReferenceValue == null)
                {
                    ShowMessage(ASSIGN_TARGET_INFO, MessageType.Info);
                    ShowDiscordButton();
                    return;
                }

                EditorGUILayout.PropertyField(useAnimation);

                targetName = targetProp.objectReferenceValue.name;
                targetName = targetName.Replace(" ", "");

                if (useAnimation.boolValue == true)
                {
                    if (useAnimation.boolValue == true)
                    {
                        EditorGUILayout.PropertyField(sourceClipsProp);
                        GUILayout.Space(5);
                    }

                    GUILayout.Space(10);

                    if (sourceClipsProp.arraySize == 0)
                    {
                        ShowMessage(ASSIGN_ANIM_INFO, MessageType.Info);
                        ShowDiscordButton();
                        return;
                    }
                    else if (sourceClipsProp.arraySize != 0)
                    {
                        if (sourceClipsProp.GetArrayElementAtIndex(0).objectReferenceValue == null)
                        {
                            ShowMessage(ASSIGN_ANIM_INFO, MessageType.Info);
                            ShowDiscordButton();
                            return;
                        }
                    }
                }

                if (useAnimation.boolValue)
                {
                    var sourceClip = (AnimationClip)sourceClipsProp.GetArrayElementAtIndex(0).objectReferenceValue;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Animation Options", EditorStyles.boldLabel);

                        var fpsProp = serializedObject.FindProperty("_framesPerSecond");
                        EditorGUILayout.PropertyField(fpsProp);

                        var previewFrameProp = serializedObject.FindProperty("_currentFrame");
                        var numFrames = (int)(sourceClip.length * fpsProp.intValue) + 1;

                        using (var changeScope = new EditorGUI.ChangeCheckScope())
                        {
                            var frame = previewFrameProp.intValue;
                            frame = EditorGUILayout.IntSlider("Current Frame", frame, 0, numFrames - 1);

                            if (changeScope.changed)
                            {
                                previewFrameProp.intValue = frame;
                                helper.SampleAnimation(frame / (float)numFrames * sourceClip.length, sourceClip);
                            }
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Capture Options", EditorStyles.boldLabel);

                    var captureCameraProp = serializedObject.FindProperty("_captureCamera");
                    EditorGUILayout.ObjectField(captureCameraProp, typeof(Camera));

                    if (captureCameraProp.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(ASSIGN_CAMERA_INFO, MessageType.Info);
                        ShowDiscordButton();
                        return;
                    }

                    var resolutionProp = serializedObject.FindProperty("_cellSize");
                    EditorGUILayout.PropertyField(resolutionProp);

                    var pivot = serializedObject.FindProperty("_pivot");
                    EditorGUILayout.PropertyField(pivot);

                    var createNormalMapProp = serializedObject.FindProperty("_createNormalMap");
                    EditorGUILayout.PropertyField(createNormalMapProp);

                    var isPixelateProp = serializedObject.FindProperty("_pixelated");
                    EditorGUILayout.PropertyField(isPixelateProp);

                    //var createAutoMaterial = serializedObject.FindProperty("_createAutoMaterial");
                    //EditorGUILayout.PropertyField(createAutoMaterial);

                    GUILayout.Space(5);

                    var spriteSavePath = serializedObject.FindProperty("_spriteSavePath");
                    /*var materialSavePath = serializedObject.FindProperty("_materialSavePath");

                    if (createAutoMaterial.boolValue == true)
                    {
                        if (GUILayout.Button("Change Material Export Location", GUILayout.Height(25)))
                        {
                            materialSavePath.stringValue = EditorUtility.OpenFolderPanel("Material Export Location", "", "");
                        }

                        if (materialSavePath.stringValue != "")
                            EditorGUILayout.LabelField(materialSavePath.stringValue, EditorStyles.miniLabel);
                    }*/

                    if (GUILayout.Button("Change Sprite Export Location", GUILayout.Height(25)))
                    {
                        spriteSavePath.stringValue = EditorUtility.OpenFolderPanel("Sprite Export Location", "", "");
                        serializedObject.ApplyModifiedProperties();
                        GUIUtility.ExitGUI();
                    }

                    if (spriteSavePath.stringValue == "" || !spriteSavePath.stringValue.Contains(Application.dataPath))
                    {
                        spriteSavePath.stringValue = Application.dataPath;
                    }

                    EditorGUILayout.LabelField(spriteSavePath.stringValue, EditorStyles.miniLabel);

                    if (spriteSavePath.stringValue.Contains(Application.dataPath))
                    {
                        if (GUILayout.Button("Capture", GUILayout.Height(40)))
                        {
                            if (useAnimation.boolValue)
                            {
                                RunRoutine(helper.CaptureAnimation(SaveCapture));
                            }
                            else
                            {
                                RunRoutine(helper.CaptureFrame(SaveCapture));
                            }
                        }
                    }
                    else
                    {
                        ShowMessage(EXPORT_LOCATION_WARNING, MessageType.Warning);
                    }

                    Texture2D previewImage = serializedObject.FindProperty("_previewImage").objectReferenceValue as Texture2D;

                    GUILayout.Space(5);
                    Rect box = GUILayoutUtility.GetAspectRect((float)previewImage.width / (float)previewImage.height, GUILayout.MinWidth(1), GUILayout.MaxWidth(9999), GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(GUILayoutUtility.GetLastRect(), previewImage);
                    GUILayout.Space(3);
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        private void ShowMessage(string message, MessageType messageType = MessageType.Info)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox(message, messageType);
        }

        private void ShowDiscordButton()
        {
            if (GUILayout.Button("Join The Community Discord Server", GUILayout.Height(40)))
            {
                Application.OpenURL("https://discord.gg/ASkVNuet8K");
            }
            GUILayout.Space(20);

            serializedObject.ApplyModifiedProperties();
        }

        private void InitiateBanner()
        {
            headerSection.x = 0;
            headerSection.y = 0;
            headerSection.width = Screen.width;
            headerSection.height = 76;

            GUI.DrawTexture(headerSection, headerTexture);

            GUILayout.Space(8);
            GUILayout.FlexibleSpace();
            GUILayout.Label(pixelateLogo, GUILayout.Width(175), GUILayout.Height(45));
            GUILayout.FlexibleSpace();
            GUILayout.Space(35);
        }

        private void RunRoutine(IEnumerator routine)
        {
            _currentCaptureRoutine = routine;
            EditorApplication.update += UpdateRoutine;
        }

        private void UpdateRoutine()
        {
            if (_currentCaptureRoutine != null)
            {
                if (!_currentCaptureRoutine.MoveNext())
                {
                    EditorApplication.update -= UpdateRoutine;
                    _currentCaptureRoutine = null;
                }
            }
        }

        private void SaveCapture(Texture2D diffuseMap, Texture2D normalMap, bool createNormalMap, bool useAnimation, bool pixelated, Vector2 cellSize, Vector2 pivot, AnimationClip animClip)
        {
            string setSpritePath = serializedObject.FindProperty("_spriteSavePath").stringValue;

            var diffusePath = setSpritePath;

            //if (setSpritePath == "")
            //    diffusePath = EditorUtility.SaveFilePanel("Save Capture", "", targetName, "png");
            //else
            //{
            //    diffusePath = EditorUtility.SaveFilePanel("Save Capture", setSpritePath, targetName, "png");
            //}

            if (string.IsNullOrEmpty(diffusePath))
            {
                return;
            }

            var fileName = targetName;
            var directory = diffusePath;
            var normalPath = "";

            if (animClip != null)
            {
                var clipName = animClip.name;
                normalPath = string.Format("{0}/{1}_{2}_{3}.{4}", directory, fileName, clipName, "NormalMap", "png");
                diffusePath = string.Format("{0}/{1}_{2}.{3}", directory, fileName, clipName, "png");
            }
            else
            {
                normalPath = string.Format("{0}/{1}_{2}.{3}", directory, fileName, "NormalMap", "png");
                diffusePath = string.Format("{0}/{1}.{2}", directory, fileName, "png");
            }

            File.WriteAllBytes(diffusePath, diffuseMap.EncodeToPNG());

            if (createNormalMap)
                File.WriteAllBytes(normalPath, normalMap.EncodeToPNG());

            AssetDatabase.Refresh();

            var diffuseAssetDirectory = diffusePath.Replace(Application.dataPath, "Assets");
            Texture2D diffuseAsset = (Texture2D)AssetDatabase.LoadAssetAtPath(diffuseAssetDirectory, typeof(Texture2D));

            AutoSpriteSlicer.Slice(diffuseAsset, cellSize, pivot, useAnimation, pixelated);

            if (createNormalMap)
            {
                normalPath = diffusePath.Remove(diffusePath.Length - 4) + "_NormalMap.png";

                var normalAssetDirectory = normalPath.Replace(Application.dataPath, "Assets");
                Texture2D normalAsset = (Texture2D)AssetDatabase.LoadAssetAtPath(normalAssetDirectory, typeof(Texture2D));

                AutoSpriteSlicer.Slice(normalAsset, cellSize, pivot, useAnimation, pixelated);
            }
        }
    }
}
