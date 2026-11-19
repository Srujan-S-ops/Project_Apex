using UnityEditor;
using UnityEditor.SceneManagement;

namespace Apex.EditorTools
{
    /// <summary>
    /// Starts Play Mode from the main menu scene. Used to run automated playtests inside the editor:
    ///   Unity -projectPath UnityGame -executeMethod Apex.EditorTools.ApexAutoPlay.Run -apexAutotest -apexScreens -apexOut DIR
    /// The autotest session exits the editor when the report has been written.
    /// </summary>
    public static class ApexAutoPlay
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
