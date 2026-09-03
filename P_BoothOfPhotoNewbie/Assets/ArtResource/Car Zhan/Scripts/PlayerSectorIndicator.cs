using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class PlayerSectorIndicator : MonoBehaviour
{
    [Header("目标主角")]
    public Transform player;

    [Header("扇形参数")]
    public float radius = 3f;
    public float angle = 90f;
    public int segments = 32;
    public float heightOffset = 0.05f;

    [Header("淡出参数")]
    public float fadeDuration = 0.5f;

    [Header("奖励系统")]
    public int minReward = 5;
    public int maxReward = 20;
    public Text scoreText;
    private int score = 0;

    [Header("倒计时与重开")]
    public Text timerText;
    public float gameDuration = 60f;
    public Text gameOverText;          // 最终分数提示
    public Text restartText;           // 重新开始提示
    public KeyCode restartKey = KeyCode.R;
    public PlayerController playerMovement; // 主角移动脚本（结束锁移动）
    private float timeLeft;
    private bool isGameOver = false;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private List<Collider> npcsInSector = new List<Collider>();

    void Awake()
    {
        // 扇形网格
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
        meshFilter.mesh = CreateSectorMesh();

        // 扇形触发碰撞体
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.mesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        // Kinematic刚体
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        timeLeft = gameDuration;

        // 开始时隐藏重新开始提示
        if (restartText != null)
            restartText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (player == null) return;

        // 扇形贴主角脚底、朝向正前方
        transform.position = player.position + Vector3.up * heightOffset;
        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);

        // F：消除扇形内NPC（结束前可用）
        if (!isGameOver && Input.GetKeyDown(KeyCode.F))
        {
            for (int i = npcsInSector.Count - 1; i >= 0; i--)
            {
                if (npcsInSector[i] != null)
                    StartCoroutine(FadeOutAndDestroy(npcsInSector[i].gameObject, fadeDuration));
            }
            npcsInSector.Clear();
        }

        // 倒计时
        if (!isGameOver)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                isGameOver = true;
                GameOver();
            }

            if (timerText != null)
            {
                int m = (int)(timeLeft / 60f);
                int s = (int)(timeLeft % 60f);
                timerText.text = string.Format("时间: {0:00}:{1:00}", m, s);
            }
        }

        // R：重新开始
        if (Input.GetKeyDown(restartKey))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // 游戏结束
    void GameOver()
    {
        if (gameOverText != null)
            gameOverText.text = "时间到！最终金币数: " + score;

        // 锁定玩家移动和转向
        if (playerMovement != null)
            playerMovement.canMove = false;

        // 弹出重新开始提示并闪烁
        if (restartText != null)
        {
            restartText.text = "按 R 重新开始";
            restartText.gameObject.SetActive(true);
            StartCoroutine(BlinkRestartText());
        }
    }

    // 重新开始提示闪烁
    IEnumerator BlinkRestartText()
    {
        while (isGameOver)
        {
            if (restartText != null)
                restartText.enabled = !restartText.enabled;
            yield return new WaitForSeconds(0.5f);
        }
    }

    // 随机金币加分并刷新UI
    void AddReward()
    {
        int coins = Random.Range(minReward, maxReward + 1);
        score += coins;
        if (scoreText != null)
            scoreText.text = "金币数量: " + score;
    }

    // NPC淡出协程
    IEnumerator FadeOutAndDestroy(GameObject npc, float duration)
    {
        Collider col = npc.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Renderer[] renderers = npc.GetComponentsInChildren<Renderer>();
        List<Material> mats = new List<Material>();
        foreach (Renderer r in renderers)
        {
            Material m = r.material;
            if (m.HasProperty("_Surface"))   // URP Lit
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else                              // Standard
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = 3000;
            mats.Add(m);
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t));
            foreach (Material m in mats)
            {
                Color c = m.color;
                c.a = a;
                m.color = c;
            }
            yield return null;
        }

        Destroy(npc);
        AddReward();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
            npcsInSector.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("NPC"))
            npcsInSector.Remove(other);
    }

    void OnDestroy()
    {
        npcsInSector.Clear();
    }

    // 程序化生成扇形网格（开口朝+Z）
    Mesh CreateSectorMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero; // 圆心

        float half = angle * 0.5f * Mathf.Deg2Rad;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(-half, half, t);
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
