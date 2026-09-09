using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    [Header("目标场景")]
    public string sceneName = "Level03";   // 在 Inspector 里填要跳转的场景名（需在 Build Settings 里）

    // 按钮点击：加载 Inspector 里填好的场景
    public void LoadSceneByName()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("SceneSwitcher：未填写场景名");
            return;
        }
        if (Application.CanStreamedLevelBeLoaded(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogWarning("场景 \"" + sceneName + "\" 不在 Build Settings 中，无法跳转");
    }

    // 按钮点击：直接传场景名加载（按钮 onClick 里手动填参数用）
    public void LoadScene(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (Application.CanStreamedLevelBeLoaded(name))
            SceneManager.LoadScene(name);
        else
            Debug.LogWarning("场景 \"" + name + "\" 不在 Build Settings 中，无法跳转");
    }

    // 按钮点击：重开当前场景
    public void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 按钮点击：退出游戏（编辑器里点=停止Play；打包后才真正退出）
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
