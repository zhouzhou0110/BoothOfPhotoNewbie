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

    [Header("飘字样式（编辑器里调）")]
    public Font uiFont;
    public int floatTextFontSize = 40;
    public FontStyle floatTextStyle = FontStyle.Bold;

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
    public Text shopText;
    public KeyCode restartKey = KeyCode.R;
    public PlayerController playerMovement;
    private float timeLeft;
    private bool isGameOver = false;

    [Header("过关条件")]
    public int targetCoins = 300;
    public Text nextLevelText;
    public KeyCode nextLevelKey = KeyCode.Y;
    public string nextSceneName = "";   // 手动指定下一关场景名（留空则自动 LevelN+1）
    private bool levelCleared = false;
    private static int currentLevel = 1;     // 当前关卡数（跨场景保留，Play停止重置）

    [Header("跨关继承（Y进下一关保留；R重开清零）")]
    private static bool carryOver = false;
    private static int carryCoins = 0;
    private static float carryExpGain = 1f;
    private static float carryRewardGain = 1f;
    private static int carryHPBonus = 0;
    private static int carryMemoryBonus = 0;
    private static float carrySpeedBonus = 0f;
    private int baseMaxHP;
    private int baseMaxMemory;
    private float baseMoveSpeed;

    [Header("商店系统")]
    public GameObject shopPanel;
    public KeyCode shopKey = KeyCode.B;
    public int lensPrice = 200;
    public int drinkPrice = 100;
    public int clockPrice = 300;
    public float angleBonusPerLens = 20f;         // 镜头：每次+扇形张开角度
    public int healAmount = 30;
    public float timeBonusPerClock = 10f;
    public Text shopMsgText;
    private bool isShopOpen = false;
    private float shopMsgTimer = 0f;
    // 设备加成（跨局保留：重开场景不丢；停止Play才重置）
    private static float bonusAngle = 0f;
    private static float bonusTime = 0f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private List<Collider> npcsInSector = new List<Collider>();

    void Awake()
    {
        // 记录本关基础值（Inspector原始值，用于跨关计算加成）
        baseMaxHP = maxHP;
        baseMaxMemory = maxMemory;
        baseMoveSpeed = (playerMovement != null) ? playerMovement.moveSpeed : 0f;

        // 跨关继承：Y进下一关保留金币+升级效果
        if (carryOver)
        {
            score = carryCoins;
            expGainMultiplier = carryExpGain;
            rewardMultiplier = carryRewardGain;
            maxHP += carryHPBonus;
            maxMemory += carryMemoryBonus;
            if (playerMovement != null)
                playerMovement.moveSpeed += carrySpeedBonus;
            carryOver = false;   // 用完即清，R重开就是全新开局
        }
        else
        {
            score = 0;
            expGainMultiplier = 1f;
            rewardMultiplier = 1f;
        }

        // 设备加成（镜头/钟表跨局保留）
        angle += bonusAngle;
        timeLeft = gameDuration + bonusTime;

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

        currentMemory = maxMemory;
        currentHP = maxHP;
        currentExp = 0;
        UpdateScoreUI();
        UpdateMemoryUI();
        UpdateHPUI();
        UpdateExpUI();

        if (restartText != null)
            restartText.gameObject.SetActive(false);
        if (shopText != null)
            shopText.gameObject.SetActive(false);
        if (nextLevelText != null)
            nextLevelText.gameObject.SetActive(false);
        if (shopPanel != null)
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
        if (!isGameOver && !isLeveling && !isShopOpen && Input.GetKeyDown(KeyCode.F))
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
                    memoryBar.gameObject.SetActive(false);
                Image bg = (memoryBar != null && memoryBar.transform.parent != null)
                    ? memoryBar.transform.parent.GetComponent<Image>() : null;
                if (bg != null)
                    bg.enabled = false;
                if (memoryText != null)
                    memoryText.text = "剩余内存容量不足!";
            }
        }

        if (warnTimer > 0f)
        {
            warnTimer -= Time.deltaTime;
            if (warnTimer <= 0f)
                UpdateMemoryUI();
        }

        // 倒计时
        if (!isGameOver && !isLeveling && !isShopOpen)
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

        // 重开（R = 全新开局）
        if (Input.GetKeyDown(restartKey))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // 游戏结束后按X打开商店
        if (isGameOver && !isShopOpen && Input.GetKeyDown(KeyCode.X))
        {
            OpenShop();
        }

        // 游戏中按B打开商店（暂停）
        if (!isGameOver && !isLeveling && !isShopOpen && Input.GetKeyDown(shopKey))
        {
            OpenShop();
        }

        // 达标通关后按Y进入下一关（保存跨关数据）
        if (isGameOver && levelCleared && Input.GetKeyDown(nextLevelKey))
        {
            carryOver = true;
            carryCoins = score;                                    // 剩余金币
            carryExpGain = expGainMultiplier;                      // 进修效果
            carryRewardGain = rewardMultiplier;                    // 报酬增加效果
            carryHPBonus = maxHP - baseMaxHP;                      // 健身效果
            carryMemoryBonus = maxMemory - baseMaxMemory;          // 内存增加效果
            carrySpeedBonus = (playerMovement != null) ? playerMovement.moveSpeed - baseMoveSpeed : 0f;  // 速度效果

            string next = GetNextSceneName();
            if (Application.CanStreamedLevelBeLoaded(next))
            {
                int parsed = ParseLevelNumber(next);
                if (parsed > 0) currentLevel = parsed;   // 更新关卡计数
                SceneManager.LoadScene(next);
            }
            // 场景不在Build Settings里：不跳转，留在本关
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
        if (score >= targetCoins)
        {
            levelCleared = true;
            EndGame("时间到！达标通关！最终金币数: " + score);
            if (nextLevelText != null)
            {
                int nextNum = ParseLevelNumber(GetNextSceneName());
                nextLevelText.text = (nextNum > 0)
                    ? "按 " + nextLevelKey + " 进入下一关"
                    : "按 " + nextLevelKey + " 进入下一关";
                nextLevelText.gameObject.SetActive(true);
            }
        }
        else
        {
            EndGame("时间到！金币数: " + score + "（目标 " + targetCoins + "）");
        }
    }

    // 下一关场景名：手动字段优先，留空自动 LevelN+1
    string GetNextSceneName()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            return nextSceneName;
        return "Level" + (currentLevel + 1);
    }

    // 从场景名里解析关卡数字（"Level03"→3，"Level01"→1），解析不了返回-1
    int ParseLevelNumber(string sceneName)
    {
        string digits = "";
        foreach (char c in sceneName)
            if (char.IsDigit(c)) digits += c;
        int n;
        return int.TryParse(digits, out n) ? n : -1;
    }

    void EndGame(string message)
    {
        if (gameOverText != null)
            gameOverText.text = message;

        if (playerMovement != null)
            playerMovement.canMove = false;

        NPCSpawner sp = FindObjectOfType<NPCSpawner>();
        if (sp != null) sp.spawning = false;

        // 游戏结束：冻结所有NPC（停止游走/追击）
        NPCController[] npcs = FindObjectsOfType<NPCController>();
        foreach (NPCController n in npcs)
            if (n != null) n.Freeze();

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

    IEnumerator BlinkRestartText()
    {
        while (isGameOver)
        {
            if (restartText != null)
                restartText.enabled = !restartText.enabled;
            if (shopText != null)
                shopText.enabled = !shopText.enabled;
            if (nextLevelText != null)
                nextLevelText.enabled = !nextLevelText.enabled;
            yield return new WaitForSeconds(0.5f);
        }
    }

    // ============ 商店 ============

    public void OpenShop()
    {
        if (isShopOpen) return;
        isShopOpen = true;

        if (playerMovement != null)
            playerMovement.canMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!isGameOver)
        {
            NPCSpawner sp = FindObjectOfType<NPCSpawner>();
            if (sp != null) sp.spawning = false;
        }

        if (shopPanel != null)
            shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (!isGameOver)
        {
            if (playerMovement != null)
                playerMovement.canMove = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            NPCSpawner sp = FindObjectOfType<NPCSpawner>();
            if (sp != null) sp.spawning = true;
        }
        else
        {
            if (restartText != null) { restartText.enabled = true; restartText.gameObject.SetActive(true); }
            if (shopText != null) { shopText.enabled = true; shopText.gameObject.SetActive(true); }
            if (nextLevelText != null) { nextLevelText.enabled = true; nextLevelText.gameObject.SetActive(true); }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // 镜头：扇形张开角度+
    public void BuyLens()
    {
        if (score < lensPrice) { ShowShopMsg("金币不足! 需要" + lensPrice + "金币"); return; }
        score -= lensPrice;
        bonusAngle += angleBonusPerLens;
        angle += angleBonusPerLens;
        RebuildSectorMesh();
        UpdateScoreUI();
        ShowShopMsg("镜头升级! 扇形张开+" + angleBonusPerLens + "°");
    }

    // 饮料：回血
    public void BuyDrink()
    {
        if (score < drinkPrice) { ShowShopMsg("金币不足! 需要" + drinkPrice + "金币"); return; }
        score -= drinkPrice;
        currentHP = Mathf.Min(currentHP + healAmount, maxHP);
        UpdateHPUI();
        UpdateScoreUI();
        ShowShopMsg("血量回复+" + healAmount);
    }

    // 钟表：增加游戏时间
    public void BuyClock()
    {
        if (score < clockPrice) { ShowShopMsg("金币不足! 需要" + clockPrice + "金币"); return; }
        score -= clockPrice;
        bonusTime += timeBonusPerClock;
        timeLeft += timeBonusPerClock;
        UpdateScoreUI();
        ShowShopMsg("时间+" + timeBonusPerClock + "秒!");
    }

    void ShowShopMsg(string msg)
    {
        if (shopMsgText != null)
        {
            shopMsgText.text = msg;
            StopCoroutine("HideShopMsg");
            StartCoroutine(HideShopMsg());
        }
    }

    IEnumerator HideShopMsg()
    {
        yield return new WaitForSeconds(1.5f);
        if (shopMsgText != null)
            shopMsgText.text = "";
    }

    void RebuildSectorMesh()
    {
        if (meshFilter != null)
            meshFilter.mesh = CreateSectorMesh();
        if (meshCollider != null)
            meshCollider.sharedMesh = meshFilter.mesh;
    }

    // ============ 其他系统 ============

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
                memoryBar.gameObject.SetActive(true);
            Image bg = (memoryBar.transform.parent != null)
                ? memoryBar.transform.parent.GetComponent<Image>() : null;
            if (bg != null && !bg.enabled)
                bg.enabled = true;
            memoryBar.type = Image.Type.Filled;
            memoryBar.fillMethod = Image.FillMethod.Horizontal;
            memoryBar.fillAmount = Mathf.Clamp01((float)currentMemory / maxMemory);
        }
        //if (memoryText != null)
        //memoryText.text = "剩余内存: " + currentMemory + "/" + maxMemory;
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

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = "金币数量: " + score;
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
        UpdateScoreUI();
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
        txt.font = (uiFont != null) ? uiFont : GetUIFont();
        txt.fontSize = floatTextFontSize;
        txt.fontStyle = floatTextStyle;
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
        NPCController npcCtrl = npc.GetComponent<NPCController>();
        if (npcCtrl != null) npcCtrl.Freeze();

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
