using System.Collections;
using Apex.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Apex.Core
{
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Race = "Race";
    }

    /// <summary>Asynchronous scene transitions behind the loading screen.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        private LoadingScreen _loading;
        public bool IsLoading { get; private set; }

        public void Load(string scene)
        {
            if (IsLoading) return;
            StartCoroutine(LoadRoutine(scene));
        }

        private IEnumerator LoadRoutine(string scene)
        {
            IsLoading = true;
            Time.timeScale = 1f;
            if (_loading == null) _loading = LoadingScreen.Create(transform);
            yield return _loading.Show(scene == SceneNames.Race);

            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                _loading.SetProgress(op.progress * 0.5f);
                yield return null;
            }
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // The scene director builds its content over a few frames and reports progress.
            float t = 0f;
            while (SceneReady.Progress < 1f && t < 30f)
            {
                _loading.SetProgress(0.5f + SceneReady.Progress * 0.5f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            _loading.SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return _loading.Hide();
            IsLoading = false;
        }
    }

    /// <summary>Scene directors report build progress here (0..1).</summary>
    public static class SceneReady
    {
        public static float Progress = 1f;
    }
}
