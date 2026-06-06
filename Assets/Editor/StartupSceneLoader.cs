using System;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class StartupSceneLoader
{
    static StartupSceneLoader()
    {
        EditorApplication.playModeStateChanged += LoadStartupScene;
    }

    private static void LoadStartupScene(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Set Bootstrap scene as the play mode start scene so Unity handles
            // unloading the editor scene gracefully, rather than calling LoadScene
            // which can break additive scene setups.
            SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                EditorBuildSettings.scenes[0].path);
            EditorSceneManager.playModeStartScene = bootstrapScene;
        }
    }
}
