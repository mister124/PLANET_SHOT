using UnityEditor;
using UnityEditor.Build.Reporting;

public static class WebGLBuild
{
    public static void Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/HomeScene.unity", "Assets/Scenes/SampleScene.unity" },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("WebGL build failed: " + report.summary.result);
    }
}
