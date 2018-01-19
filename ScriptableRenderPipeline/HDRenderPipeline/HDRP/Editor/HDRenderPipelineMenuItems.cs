﻿using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEngine.SceneManagement;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    using UnityObject = UnityEngine.Object;

    public class HDRenderPipelineMenuItems
    {
        [MenuItem("Internal/HDRenderPipeline/Add \"Additional Light-shadow Data\" (if not present)")]
        static void AddAdditionalLightData()
        {
            var lights = UnityObject.FindObjectsOfType(typeof(Light)) as Light[];

            foreach (var light in lights)
            {
                // Do not add a component if there already is one.
                if (light.GetComponent<HDAdditionalLightData>() == null)
                    light.gameObject.AddComponent<HDAdditionalLightData>();

                if (light.GetComponent<AdditionalShadowData>() == null)
                    light.gameObject.AddComponent<AdditionalShadowData>();
            }
        }

        [MenuItem("Internal/HDRenderPipeline/Add \"Additional Camera Data\" (if not present)")]
        static void AddAdditionalCameraData()
        {
            var cameras = UnityObject.FindObjectsOfType(typeof(Camera)) as Camera[];

            foreach (var camera in cameras)
            {
                // Do not add a component if there already is one.
                if (camera.GetComponent<HDAdditionalCameraData>() == null)
                    camera.gameObject.AddComponent<HDAdditionalCameraData>();
            }
        }
        static void CheckOutFile(bool VSCEnabled, UnityObject mat)
        {
            if (VSCEnabled)
            {
                UnityEditor.VersionControl.Task task = UnityEditor.VersionControl.Provider.Checkout(mat, UnityEditor.VersionControl.CheckoutMode.Both);

                if (!task.success)
                {
                    Debug.Log(task.text + " " + task.resultCode);
                }
            }
        }

        // This script is a helper for the artists to re-synchronize all layered materials
        [MenuItem("Internal/HDRenderPipeline/Synchronize all Layered materials")]
        static void SynchronizeAllLayeredMaterial()
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>();

            bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

            foreach (var mat in materials)
            {
                if (mat.shader.name == "HDRenderPipeline/LayeredLit" || mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
                {
                    CheckOutFile(VSCEnabled, mat);
                    LayeredLitGUI.SynchronizeAllLayers(mat);
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        // The goal of this script is to help maintenance of data that have already been produced but need to update to the latest shader code change.
        // In case the shader code have change and the inspector have been update with new kind of keywords we need to regenerate the set of keywords use by the material.
        // This script will remove all keyword of a material and trigger the inspector that will re-setup all the used keywords.
        // It require that the inspector of the material have a static function call that update all keyword based on material properties.
        [MenuItem("Edit/Render Pipeline/Upgrade/High Definition/Reset All Materials Keywords (Loaded Materials)", priority = CoreUtils.editMenuPriority2)]
        static void ResetAllMaterialKeywords()
        {
            try
            {
                ResetAllLoadedMaterialKeywords(string.Empty, 1, 0);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Edit/Render Pipeline/Upgrade/High Definition/Reset All Material Asset's Keywords (Materials in Project)", priority = CoreUtils.editMenuPriority2)]
        static void ResetAllMaterialAssetsKeywords()
        {
            try
            {
                ResetAllMaterialAssetsKeywords(1, 0);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Edit/Render Pipeline/Upgrade/High Definition/Reset All Materials Keywords (Materials in Project and scenes)", priority = CoreUtils.editMenuPriority2)]
        static void ResetAllMaterialKeywordsInProjectAndScenes()
        {
            var openedScenes = new string[EditorSceneManager.loadedSceneCount];
            for (var i = 0; i < openedScenes.Length; ++i)
                openedScenes[i] = SceneManager.GetSceneAt(i).path;

            bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

            try
            {
                var scenes = AssetDatabase.FindAssets("t:Scene");
                var scale = 1f / Mathf.Max(1, scenes.Length);
                for (var i = 0; i < scenes.Length; ++i)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(scenes[i]);
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                    CheckOutFile(VSCEnabled, sceneAsset);
                    EditorSceneManager.OpenScene(scenePath);

                    var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    var description = string.Format("{0} {1}/{2} - ", sceneName, i + 1, scenes.Length);

                    ResetAllLoadedMaterialKeywords(description, scale, scale * i);
                }

                ResetAllMaterialAssetsKeywords(scale, scale * (scenes.Length - 1));
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (openedScenes.Length > 0)
                {
                    EditorSceneManager.OpenScene(openedScenes[0]);
                    for (var i = 1; i < openedScenes.Length; i++)
                        EditorSceneManager.OpenScene(openedScenes[i], OpenSceneMode.Additive);
                }
            }
        }

        [MenuItem("Internal/HDRenderPipeline/Update/Update SSS profile indices")]
        static void UpdateSSSProfileIndices()
        {
            try
            {
                var matIds = AssetDatabase.FindAssets("t:Material");

                for (int i = 0, length = matIds.Length; i < length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(matIds[i]);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                    EditorUtility.DisplayProgressBar(
                        "Setup materials Keywords...",
                        string.Format("{0} / {1} materials SSS updated.", i, length),
                        i / (float)(length - 1));

                    bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

                    if (mat.shader.name == "HDRenderPipeline/LitTessellation" ||
                        mat.shader.name == "HDRenderPipeline/Lit")
                    {
                        if (mat.HasProperty("_SubsurfaceProfile"))
                        {
                            CheckOutFile(VSCEnabled, mat);
                            //float value = mat.GetInt("_DiffusionProfile");
                            //mat.SetInt("_DiffusionProfile", 0);

                            EditorUtility.SetDirty(mat);
                        }
                    }
                    else if (mat.shader.name == "HDRenderPipeline/LayeredLit" ||
                                mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
                    {
                        bool hasSubsurfaceProfile = false;

                        int numLayer = (int)mat.GetFloat("_LayerCount");

                        for (int x = 0; x < numLayer; ++x)
                        {
                            if (mat.HasProperty("_SubsurfaceProfile" + x))
                            {
                                hasSubsurfaceProfile = true;
                            }
                        }

                        if (hasSubsurfaceProfile)
                        {
                            CheckOutFile(VSCEnabled, mat);

                            for (int x = 0; x < numLayer; ++x)
                            {
                                if (mat.HasProperty("_SubsurfaceProfile" + x))
                                {
                                    CheckOutFile(VSCEnabled, mat);
                                    //float value = mat.GetInt("_DiffusionProfile" + x);
                                    //mat.SetInt("_DiffusionProfile" + x, 0);

                                    EditorUtility.SetDirty(mat);
                                }
                            }

                            EditorUtility.SetDirty(mat);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        //
        [MenuItem("Internal/HDRenderPipeline/Update/Update Height Maps parametrization")]
        static void UpdateHeightMapParametrization()
        {
            try
            {
                var matIds = AssetDatabase.FindAssets("t:Material");

                for (int i = 0, length = matIds.Length; i < length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(matIds[i]);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                    EditorUtility.DisplayProgressBar(
                        "Updating Materials...",
                        string.Format("{0} / {1} materials updated.", i, length),
                        i / (float)(length - 1));

                    bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

                    if (mat.shader.name == "HDRenderPipeline/LitTessellation" ||
                        mat.shader.name == "HDRenderPipeline/Lit")
                    {
                        // Need only test one of the new properties
                        if (mat.HasProperty("_HeightPoMAmplitude"))
                        {
                            CheckOutFile(VSCEnabled, mat);

                            float valueMax = mat.GetFloat("_HeightMax");
                            float valueMin = mat.GetFloat("_HeightMin");
                            float center = mat.GetFloat("_HeightCenter");
                            float amplitude = valueMax - valueMin;
                            mat.SetInt("_HeightMapParametrization", 1);
                            mat.SetFloat("_HeightPoMAmplitude", amplitude);
                            mat.SetFloat("_HeightTessAmplitude", amplitude);
                            mat.SetFloat("_HeightOffset", 0.0f);
                            mat.SetFloat("_HeightTessCenter", center);

                            BaseLitGUI.DisplacementMode displaceMode = (BaseLitGUI.DisplacementMode)mat.GetInt("_DisplacementMode");
                            if (displaceMode == BaseLitGUI.DisplacementMode.Pixel)
                            {
                                mat.SetFloat("_HeightCenter", 1.0f); // With PoM this is always 1.0f. We set it here to avoid having to open the UI to update it.
                            }

                            EditorUtility.SetDirty(mat);
                        }
                    }
                    else if (mat.shader.name == "HDRenderPipeline/LayeredLit" ||
                                mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
                    {
                        int numLayer = (int)mat.GetFloat("_LayerCount");

                        if (mat.HasProperty("_HeightPoMAmplitude0"))
                        {
                            CheckOutFile(VSCEnabled, mat);

                            for (int x = 0; x < numLayer; ++x)
                            {
                                float valueMax = mat.GetFloat("_HeightMax" + x);
                                float valueMin = mat.GetFloat("_HeightMin" + x);
                                float center = mat.GetFloat("_HeightCenter" + x);
                                float amplitude = valueMax - valueMin;
                                mat.SetInt("_HeightMapParametrization" + x, 1);
                                mat.SetFloat("_HeightPoMAmplitude" + x, amplitude);
                                mat.SetFloat("_HeightTessAmplitude" + x, amplitude);
                                mat.SetFloat("_HeightOffset" + x, 0.0f);
                                mat.SetFloat("_HeightTessCenter" + x, center);

                                BaseLitGUI.DisplacementMode displaceMode = (BaseLitGUI.DisplacementMode)mat.GetInt("_DisplacementMode");
                                if (displaceMode == BaseLitGUI.DisplacementMode.Pixel)
                                {
                                    mat.SetFloat("_HeightCenter" + x, 1.0f); // With PoM this is always 1.0f. We set it here to avoid having to open the UI to update it.
                                }
                            }

                            EditorUtility.SetDirty(mat);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Function used only to check performance of data with and without tessellation
        [MenuItem("Internal/HDRenderPipeline/Test/Remove tessellation materials (not reversible)")]
        static void RemoveTessellationMaterials()
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>();

            var litShader = Shader.Find("HDRenderPipeline/Lit");
            var layeredLitShader = Shader.Find("HDRenderPipeline/LayeredLit");

            foreach (var mat in materials)
            {
                if (mat.shader.name == "HDRenderPipeline/LitTessellation")
                {
                    mat.shader = litShader;
                    // We remove all keyword already present
                    CoreEditorUtils.RemoveMaterialKeywords(mat);
                    LitGUI.SetupMaterialKeywordsAndPass(mat);
                    EditorUtility.SetDirty(mat);
                }
                else if (mat.shader.name == "HDRenderPipeline/LayeredLitTessellation")
                {
                    mat.shader = layeredLitShader;
                    // We remove all keyword already present
                    CoreEditorUtils.RemoveMaterialKeywords(mat);
                    LayeredLitGUI.SetupMaterialKeywordsAndPass(mat);
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        [MenuItem("Edit/Render Pipeline/Tools/High Definition/Export Sky to Image", priority = CoreUtils.editMenuPriority2)]
        static void ExportSkyToImage()
        {
            var renderpipeline = RenderPipelineManager.currentPipeline as HDRenderPipeline;
            if (renderpipeline == null)
            {
                Debug.LogError("HDRenderPipeline is not instantiated.");
                return;
            }

            var result = renderpipeline.ExportSkyToTexture();
            if (result == null)
                return;

            // Encode texture into PNG
            byte[] bytes = result.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
            UnityObject.DestroyImmediate(result);

            string assetPath = EditorUtility.SaveFilePanel("Export Sky", "Assets", "SkyExport", "exr");
            if (!string.IsNullOrEmpty(assetPath))
            {
                File.WriteAllBytes(assetPath, bytes);
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("GameObject/Render Pipeline/High Definition/Scene Settings", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateCustomGameObject(MenuCommand menuCommand)
        {
            var sceneSettings = new GameObject("Scene Settings");
            GameObjectUtility.SetParentAndAlign(sceneSettings, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(sceneSettings, "Create " + sceneSettings.name);
            Selection.activeObject = sceneSettings;

            var profile = VolumeProfileFactory.CreateVolumeProfile(sceneSettings.scene, "Scene Settings");
            VolumeProfileFactory.CreateVolumeComponent<HDShadowSettings>(profile, true, false);
            var visualEnv = VolumeProfileFactory.CreateVolumeComponent<VisualEnvironment>(profile, true, false);
            visualEnv.skyType.value = SkySettings.GetUniqueID<ProceduralSky>();
            visualEnv.fogType.value = FogType.Exponential;
            VolumeProfileFactory.CreateVolumeComponent<ProceduralSky>(profile, true, false);
            VolumeProfileFactory.CreateVolumeComponent<ExponentialFog>(profile, true, true);

            var volume = sceneSettings.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var bakingSky = sceneSettings.AddComponent<BakingSky>();
            bakingSky.profile = volume.sharedProfile;
            bakingSky.bakingSkyUniqueID = SkySettings.GetUniqueID<ProceduralSky>();
        }

        class DoCreateNewAsset<TAssetType> : ProjectWindowCallback.EndNameEditAction where TAssetType : ScriptableObject
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var newAsset  = CreateInstance<TAssetType>();
                newAsset.name = Path.GetFileName(pathName);
                AssetDatabase.CreateAsset(newAsset, pathName);
                ProjectWindowUtil.ShowCreatedAsset(newAsset);
            }
        }

        class DoCreateNewAssetDiffusionProfileSettings : DoCreateNewAsset<DiffusionProfileSettings> {}

        [MenuItem("Assets/Create/Render Pipeline/High Definition/Diffusion profile Settings", priority = CoreUtils.assetCreateMenuPriority2)]
        static void MenuCreateDiffusionProfile()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateNewAssetDiffusionProfileSettings>(), "New Diffusion Profile Settings.asset", icon, null);
        }

        static void ResetAllMaterialAssetsKeywords(float progressScale, float progressOffset)
        {
            var matIds = AssetDatabase.FindAssets("t:Material");

            bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

            for (int i = 0, length = matIds.Length; i < length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(matIds[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                EditorUtility.DisplayProgressBar(
                    "Setup material asset's Keywords...",
                    string.Format("{0} / {1} materials cleaned.", i, length),
                    (i / (float)(length - 1)) * progressScale + progressOffset);

                CheckOutFile(VSCEnabled, mat);
                var h = Debug.unityLogger.logHandler;
                Debug.unityLogger.logHandler = new UnityContextualLogHandler(mat);
                HDEditorUtils.ResetMaterialKeywords(mat);
                Debug.unityLogger.logHandler = h;
            }
        }

        static void ResetAllLoadedMaterialKeywords(string descriptionPrefix, float progressScale, float progressOffset)
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>();

            bool VSCEnabled = (UnityEditor.VersionControl.Provider.enabled && UnityEditor.VersionControl.Provider.isActive);

            for (int i = 0, length = materials.Length; i < length; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Setup materials Keywords...",
                    string.Format("{0}{1} / {2} materials cleaned.", descriptionPrefix, i, length),
                    (i / (float)(length - 1)) * progressScale + progressOffset);

                CheckOutFile(VSCEnabled, materials[i]);
                HDEditorUtils.ResetMaterialKeywords(materials[i]);
            }
        }

        class UnityContextualLogHandler : ILogHandler
        {
            UnityObject m_Context;
            static readonly ILogHandler k_DefaultLogHandler = Debug.unityLogger.logHandler;

            public UnityContextualLogHandler(UnityObject context)
            {
                m_Context = context;
            }

            public void LogFormat(LogType logType, UnityObject context, string format, params object[] args)
            {
                k_DefaultLogHandler.LogFormat(LogType.Log, m_Context, "Context: {0} ({1})", m_Context, AssetDatabase.GetAssetPath(m_Context));
                k_DefaultLogHandler.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityObject context)
            {
                k_DefaultLogHandler.LogFormat(LogType.Log, m_Context, "Context: {0} ({1})", m_Context, AssetDatabase.GetAssetPath(m_Context));
                k_DefaultLogHandler.LogException(exception, context);
            }
        }
    }
}
