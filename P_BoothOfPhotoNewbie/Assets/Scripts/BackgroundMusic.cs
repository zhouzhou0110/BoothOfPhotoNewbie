using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    [Header("背景音乐")]
    public AudioClip musicClip;

    [Header("音量（0~1）")]
    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Header("自动播放")]
    public bool playOnAwake = true;

    private static BackgroundMusic instance;
    private AudioSource audioSource;

    void Awake()
    {
        // 只保留一个实例，防止切场景/重复挂载导致音乐叠放
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);   // 跨场景不中断

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
        audioSource.clip = musicClip;

        if (playOnAwake && musicClip != null)
            audioSource.Play();
    }
}
