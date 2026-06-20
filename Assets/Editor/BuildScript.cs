using UnityEditor;

public class BuildScript
{
    public static void BuildWebGL()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GameWithEnd.unity" },
            locationPathName = "finalitchbuild",
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
    }
}
