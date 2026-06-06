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

            SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                EditorBuildSettings.scenes[0].path);
            EditorSceneManager.playModeStartScene = bootstrapScene;
        }
    }
}
