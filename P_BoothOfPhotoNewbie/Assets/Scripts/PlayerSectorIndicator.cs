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

    [Header("奖励兜底（NPC没挂Controller时用）")]
    public int minReward = 5;
    public int maxReward = 20;
    public Text scoreText;
    private int score = 0;

    [Header("经验值系统")]
    public int expPerShot = 10;
    public int maxExp = 100;
    public Image expBar;
    public Text expText;
    private int currentExp = 0;

    [Header("升级系统")]
    public GameObject levelUpPanel;
    private bool isLeveling = false;

    [Header("升级技能（随机3选1，可不拖自动按名字找）")]
    public GameObject[] skillButtons;
    private List<int> offeredSkills = new List<int>();

    [Header("技能数值（可调）")]
    public float expGainBonus = 0.2f;
    public int fitnessHPBonus = 20;
    public float rewardBonus = 0.2f;
    public int memoryBonus = 10;
    public float speedBonus = 1f;
    private float expGainMultiplier = 1f;
    private float rewardMultiplier = 1f;

    [Header("内存条（胶卷）")]
    public int maxMemory = 50;
    public int memoryCostPerShot = 10;
    public Image memoryBar;
    public Text memoryText;
    private int currentMemory;
    private float warnTimer = 0f;

    [Header("血量系统")]
    public int maxHP = 100;
    public Image hpBar;
    public Text hpText;
    private int currentHP;

    [Header("倒计时与重开")]
    public Text timerText;
    public float gameDuration = 60f;
    public Text gameOverText;
    public Text restartText;
    public Text shopText;                    // 进入商店提示文本
    public GameObject shopPanel;             // ← 新增：商店面板（平时隐藏）
    private bool isShopOpen = false;         // ← 新增：商店是否已打开
    public KeyCode restartKey = KeyCode.R;
    public PlayerController playerMovement;
    private float timeLeft;
    private bool isGameOver = false;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private List<Collider> npcsInSector = new List<Collider>();

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
        if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
        meshFilter.mesh = CreateSectorMesh();

        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.mesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        timeLeft = gameDuration;
        currentMemory = maxMemory;
        currentHP = maxHP;
        currentExp = 0;
        expGainMultiplier = 1f;
        rewardMultiplier = 1f;
        UpdateMemoryUI();
        UpdateHPUI();
        UpdateExpUI();

        if (restartText != null)
            restartText.gameObject.SetActive(false);
        if (shopText != null)
            shopText.gameObject.SetActive(false);
        if (shopPanel != null)               // ← 新增
            shopPanel.gameObject.SetActive(false);


        if (levelUpPanel != null)
            levelUpPanel.gameObject.SetActive(false);

        GameObject[] buttons = GetSkillButtons();
        if (buttons != null)
        {
            foreach (GameObject b in buttons)
                if (b != null) b.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null) return;

        transform.position = player.position + Vector3.up * heightOffset;
        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);

        // F：拍摄扇形内最近1个NPC
        if (!isGameOver && !isLeveling && Input.GetKeyDown(KeyCode.F))
        {
            if (currentMemory >= memoryCostPerShot)
            {
                Collider target = GetNearestNPC();
                if (target != null)
                {
                    currentMemory -= memoryCostPerShot;
                    UpdateMemoryUI();
                    npcsInSector.Remove(target);

                    NPCController npc = target.GetComponent<NPCController>();
                    if (npc != null)
                    {
                        int outcome = npc.RollOutcome();
                        if (outcome == 2)
                        {
                            npc.TriggerAngry();
                            StartCoroutine(SpawnFloatText(target.transform.position, "生气!", new Color(1f, 0.3f, 0.3f)));
                        }
                        else
                        {
                            int rewardCoins = GetRewardWithMultiplier(npc.GetReward(outcome));
                            AddExp(GetExpGain());
                            string msg = (outcome == 0)
                                ? "+" + rewardCoins + " 出片!"
                                : "+" + rewardCoins + " 普通";
                            Color col = (outcome == 0)
                                ? new Color(1f, 0.84f, 0f)
                                : Color.white;
                            StartCoroutine(SpawnFloatText(target.transform.position, msg, col));
                            StartCoroutine(FadeOutAndDestroy(target.gameObject, fadeDuration, rewardCoins));
                        }
                    }
                    else
                    {
                        int rewardCoins = GetRewardWithMultiplier(Random.Range(minReward, maxReward + 1));
                        AddExp(GetExpGain());
                        StartCoroutine(SpawnFloatText(target.transform.position, "+" + rewardCoins, Color.white));
                        StartCoroutine(FadeOutAndDestroy(target.gameObject, fadeDuration, rewardCoins));
                    }
                }
            }
            else
            {
                warnTimer = 60f;
                if (memoryBar != null)
                    memoryBar.gameObject.SetActive(false);   // 隐藏蓝色填充条
                Image bg = (memoryBar != null && memoryBar.transform.parent != null)
                    ? memoryBar.transform.parent.GetComponent<Image>() : null;
                if (bg != null)
                    bg.enabled = false;                      // ← 新增：同时隐藏灰色背景框
                if (memoryText != null)
                    memoryText.text = "内存不足!";
            }


        }

        if (warnTimer > 0f)
        {
            warnTimer -= Time.deltaTime;
            if (warnTimer <= 0f)
                UpdateMemoryUI();
        }

        // 倒计时
        if (!isGameOver && !isLeveling)
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

        // 重开
        if (Input.GetKeyDown(restartKey))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // 进入商店（仅游戏结束后）
        // 打开商店面板（仅游戏结束后按X）
        if (isGameOver && !isShopOpen && Input.GetKeyDown(KeyCode.X))
        {
            OpenShop();
        }

    }

    public void TakeDamage(int dmg)
    {
        if (isGameOver) return;
        currentHP -= dmg;
        if (currentHP < 0) currentHP = 0;
        UpdateHPUI();
        if (currentHP <= 0)
            FailGame();
    }

    void FailGame()
    {
        isGameOver = true;
        EndGame("燃尽了！最终金币数: " + score);
    }

    void GameOver()
    {
        EndGame("时间到！最终金币数: " + score);
    }

    void EndGame(string message)
    {
        if (gameOverText != null)
            gameOverText.text = message;

        if (playerMovement != null)
            playerMovement.canMove = false;

        NPCSpawner sp = FindObjectOfType<NPCSpawner>();
        if (sp != null) sp.spawning = false;

        if (restartText != null)
        {
            restartText.text = "按 R 重新开始";
            restartText.gameObject.SetActive(true);
            StartCoroutine(BlinkRestartText());
        }

        if (shopText != null)
        {
            shopText.text = "按 X 进入商店";
            shopText.gameObject.SetActive(true);
        }
    }
    // 打开商店面板（游戏结束后按X触发）
    public void OpenShop()
    {
        if (!isGameOver || isShopOpen) return;
        isShopOpen = true;

        // 隐藏结束提示文本，避免和面板叠在一起
        if (restartText != null) restartText.gameObject.SetActive(false);
        if (shopText != null) shopText.gameObject.SetActive(false);

        // 解锁鼠标（面板上要点按钮）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (shopPanel != null)
            shopPanel.SetActive(true);
    }

    // 关闭商店面板，返回结束界面
    public void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        // 恢复结束提示（重置 enabled，防止闪烁协程停在隐藏状态）
        if (restartText != null) { restartText.enabled = true; restartText.gameObject.SetActive(true); }
        if (shopText != null) { shopText.enabled = true; shopText.gameObject.SetActive(true); }

        // 结束界面鼠标保持解锁
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    IEnumerator BlinkRestartText()
    {
        while (isGameOver)
        {
            if (restartText != null)
                restartText.enabled = !restartText.enabled;
            if (shopText != null)
                shopText.enabled = !shopText.enabled;
            yield return new WaitForSeconds(0.5f);
        }
    }

    Collider GetNearestNPC()
    {
        Collider nearest = null;
        float minDist = float.MaxValue;
        for (int i = npcsInSector.Count - 1; i >= 0; i--)
        {
            if (npcsInSector[i] == null) { npcsInSector.RemoveAt(i); continue; }
            float d = Vector3.Distance(npcsInSector[i].transform.position, player.position);
            if (d < minDist) { minDist = d; nearest = npcsInSector[i]; }
        }
        return nearest;
    }

    void UpdateMemoryUI()
    {
        if (memoryBar != null)
        {
            if (!memoryBar.gameObject.activeSelf)
                memoryBar.gameObject.SetActive(true);        // 恢复填充条
            Image bg = (memoryBar.transform.parent != null)
                ? memoryBar.transform.parent.GetComponent<Image>() : null;
            if (bg != null && !bg.enabled)
                bg.enabled = true;                           // ← 新增：恢复背景框
            memoryBar.type = Image.Type.Filled;
            memoryBar.fillMethod = Image.FillMethod.Horizontal;
            memoryBar.fillAmount = Mathf.Clamp01((float)currentMemory / maxMemory);
        }
            // if (memoryText != null)
            //memoryText.text = "内存容量: " + currentMemory + "/" + maxMemory;
    }



    void UpdateHPUI()
    {
        if (hpBar != null)
        {
            hpBar.type = Image.Type.Filled;
            hpBar.fillMethod = Image.FillMethod.Horizontal;
            hpBar.fillAmount = Mathf.Clamp01((float)currentHP / maxHP);
        }
        if (hpText != null)
            hpText.text = "血量: " + currentHP + "/" + maxHP;
    }

    void UpdateExpUI()
    {
        if (expBar != null)
        {
            expBar.type = Image.Type.Filled;
            expBar.fillMethod = Image.FillMethod.Horizontal;
            expBar.fillAmount = Mathf.Clamp01((float)currentExp / maxExp);
        }
        if (expText != null)
            expText.text = "经验: " + currentExp + "/" + maxExp;
    }

    int GetExpGain()
    {
        return Mathf.RoundToInt(expPerShot * expGainMultiplier);
    }

    int GetRewardWithMultiplier(int baseReward)
    {
        return Mathf.RoundToInt(baseReward * rewardMultiplier);
    }

    void AddExp(int amount)
    {
        currentExp += amount;
        UpdateExpUI();
        if (currentExp >= maxExp && !isLeveling)
            LevelUp();
    }

    void LevelUp()
    {
        isLeveling = true;

        if (playerMovement != null)
            playerMovement.canMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        NPCSpawner sp = FindObjectOfType<NPCSpawner>();
        if (sp != null) sp.spawning = false;

        RandomizeSkillButtons();

        if (levelUpPanel != null)
            levelUpPanel.SetActive(true);
    }

    void RandomizeSkillButtons()
    {
        GameObject[] buttons = GetSkillButtons();
        if (buttons == null) return;

        List<int> pool = new List<int>() { 0, 1, 2, 3, 4 };
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }
        offeredSkills = pool.GetRange(0, 3);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].SetActive(offeredSkills.Contains(i));
        }
    }

    GameObject[] GetSkillButtons()
    {
        if (skillButtons != null && skillButtons.Length == 5)
            return skillButtons;

        if (levelUpPanel != null)
        {
            GameObject[] arr = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                Transform t = levelUpPanel.transform.Find("SkillButton" + i);
                arr[i] = (t != null) ? t.gameObject : null;
            }
            return arr;
        }
        return null;
    }

    public void ChooseSkill(int skillIndex)
    {
        if (!isLeveling) return;
        if (offeredSkills.Count > 0 && !offeredSkills.Contains(skillIndex)) return;

        switch (skillIndex)
        {
            case 0:
                expGainMultiplier += expGainBonus;
                break;
            case 1:
                maxHP += fitnessHPBonus;
                currentHP = maxHP;
                break;
            case 2:
                rewardMultiplier += rewardBonus;
                break;
            case 3:
                maxMemory += memoryBonus;
                currentMemory = Mathf.Min(currentMemory + memoryBonus, maxMemory);
                break;
            case 4:
                if (playerMovement != null)
                    playerMovement.moveSpeed += speedBonus;
                break;
        }

        UpdateMemoryUI();
        UpdateHPUI();
        CloseLevelUpPanel();
    }

    public void CloseLevelUpPanel()
    {
        if (levelUpPanel != null)
            levelUpPanel.SetActive(false);

        GameObject[] buttons = GetSkillButtons();
        if (buttons != null)
        {
            foreach (GameObject b in buttons)
                if (b != null) b.SetActive(false);
        }

        if (playerMovement != null)
            playerMovement.canMove = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        NPCSpawner sp = FindObjectOfType<NPCSpawner>();
        if (sp != null) sp.spawning = true;

        currentExp -= maxExp;
        if (currentExp < 0) currentExp = 0;
        UpdateExpUI();

        isLeveling = false;
    }

    void AddReward(int coins)
    {
        score += coins;
        if (scoreText != null)
            scoreText.text = "金币数量: " + score;
    }

    Font GetUIFont()
    {
        Font f = null;
        try { f = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei", "SimHei", "Arial" }, 60); } catch { }
        return f;
    }

    IEnumerator SpawnFloatText(Vector3 worldPos, string msg, Color color)
    {
        GameObject canvasObj = new GameObject("FloatText");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.transform.position = worldPos + Vector3.up * 0.8f;
        canvasObj.transform.localScale = Vector3.one * 0.01f;
        if (Camera.main != null)
            canvasObj.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        Text txt = textObj.AddComponent<Text>();
        txt.text = msg;
        txt.font = GetUIFont();
        txt.fontSize = 40;
        txt.fontStyle = FontStyle.Bold;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 80);

        float t = 0f;
        float duration = 1.2f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            canvasObj.transform.position += Vector3.up * Time.deltaTime * 1.2f;
            txt.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }
        Destroy(canvasObj);
    }

    IEnumerator FadeOutAndDestroy(GameObject npc, float duration, int rewardCoins)
    {
        Collider col = npc.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Renderer[] renderers = npc.GetComponentsInChildren<Renderer>();
        List<Material> mats = new List<Material>();
        foreach (Renderer r in renderers)
        {
            Material m = r.material;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
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
        AddReward(rewardCoins);
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

    Mesh CreateSectorMesh()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
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
