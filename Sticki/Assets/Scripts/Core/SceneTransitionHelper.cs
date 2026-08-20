using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sticki.Core
{
    public class SceneTransitionHelper : MonoBehaviour
    {
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;
            Debug.Log($"SceneTransitionHelper: Loading scene '{sceneName}'");
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public void LoadNextArena()
        {
            Debug.Log("SceneTransitionHelper: Loading next arena via RunFlowController.");
            Time.timeScale = 1f;
            RunFlowController.Instance.LoadNextArena();
        }

        public void QuitGame()
{
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
