using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    [Header("要控制的面板")]
    public GameObject panel;

    [Header("是否开局隐藏（勾选=开局自动隐藏）")]
    public bool startHidden = true;

    void Start()
    {
        if (panel != null && startHidden)
            panel.SetActive(false);
    }

    // 按钮点击：切换面板 显示/隐藏
    public void TogglePanel()
    {
        if (panel == null)
        {
            Debug.LogWarning("PanelToggle：未拖入面板");
            return;
        }
        panel.SetActive(!panel.activeSelf);
    }

    // 按钮点击：显示面板
    public void ShowPanel()
    {
        if (panel != null) panel.SetActive(true);
    }

    // 按钮点击：隐藏面板
    public void HidePanel()
    {
        if (panel != null) panel.SetActive(false);
    }
}
