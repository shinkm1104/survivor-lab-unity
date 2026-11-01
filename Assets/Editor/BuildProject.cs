using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurvivorLab.Editor {
    public static class BuildProject {
        [MenuItem("Survivor Lab/Build Windows")]
        public static void Build() { BuildPlayer(false); }
        public static void BuildDevelopment() { BuildPlayer(true); }
        static void BuildPlayer(bool development) {
            QualityChecks.Run(Balance.Load());
            PlayerSettings.companyName="Independent Lab"; PlayerSettings.productName="Survivor Lab";
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Game").AddComponent<GameApp>();
            var mat=new Material(Shader.Find("Sprites/Default"));
            if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/GameMaterial.mat")==null) AssetDatabase.CreateAsset(mat,"Assets/Resources/GameMaterial.mat");
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName="Builds/Windows/SurvivorLab.exe",target=BuildTarget.StandaloneWindows64,options=development?BuildOptions.Development:BuildOptions.None });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed: "+report.summary.result);
            if(File.Exists("WINDOWS_README.md")) File.Copy("WINDOWS_README.md","Builds/Windows/README.md",true);
            Debug.Log("WINDOWS BUILD SUCCESS: "+Path.GetFullPath("Builds/Windows/SurvivorLab.exe"));
        }
        public static void Verify() { QualityChecks.Run(Balance.Load()); }
    }
}
