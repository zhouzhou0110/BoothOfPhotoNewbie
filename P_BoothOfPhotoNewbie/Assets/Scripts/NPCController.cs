using UnityEngine;
using System.Collections;

public class NPCController : MonoBehaviour
{
    [Header("出片结果")]
    public float goodShotChance = 0.4f;  // 出片率（0~1）
    public int goodReward = 15;          // 出片金币

    [Header("普通结果")]
    public float normalChance = 0.55f;   // 普通率
    public int normalReward = 10;        // 普通金币

    [Header("生气结果")]
    public float angryChance = 0.05f;    // 生气率
    public float chaseSpeed = 6f;        // 追击速度
    public int damageOnHit = 10;         // 追到扣血

    [Header("生气追击延迟")]
    public float angryDelay = 1f;        // 生气后延迟几秒再追击（编辑器可调）
    private bool canChase = false;       // 延迟结束后才可追击

    [Header("生气外观")]
    [Range(0f, 1f)]
    public float angryAlpha = 0.5f;      // 生气变红的透明度（0完全透明 ~ 1不透明），Inspector可调

    [Header("随机游走（出生点附近活动）")]
    public float wanderSpeed = 2f;       // 游走速度（比追击慢）
    public float wanderRadius = 4f;      // 以出生点为中心的活动范围（半径）
    public float changeInterval = 2f;    // 每隔几秒换一个方向
    [Range(0f, 1f)]
    public float idleChance = 0.25f;     // 换方向时发呆（原地停一下）的概率

    [Header("移动动画（Move.controller 的 Speed 参数）")]
    public Animator animator;                    // 不拖就自动找子物体上的Animator
    public string speedParamName = "Speed";      // 控制器里的速度参数名
    [Range(0.05f, 0.5f)]
    public float animSpeedScale = 0.2f;          // 实际速度→动画参数换算（2游走→0.4走，6追击→1.2跑）

    [Header("生气音效（男女不同）")]
    public AudioClip angryMaleClip;     // 男生气音效（Inspector里拖）
    public AudioClip angryFemaleClip;   // 女生气音效（Inspector里拖）
    public AudioSource angrySource;     // 可不拖，自动创建
    [Tooltip("该NPC是女性模型？决定生气时播放哪个音效")]
    public bool isFemale = false;       // 在对应NPC预制体Inspector里勾选

    private bool isAngry = false;
    private bool frozen = false;         // 冻结：淡出/游戏结束时置true，停止一切移动
    private Transform player;
    private PlayerSectorIndicator game;

    // 游走状态
    private Vector3 homePos;             // 出生点（活动中心）
    private Vector3 wanderDir;           // 当前游走方向
    private float changeTimer = 0f;
    private bool isMoving = true;        // false = 发呆阶段

    // 动画状态
    private bool hasAnim = false;
    private int speedParamHash = 0;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        game = FindObjectOfType<PlayerSectorIndicator>();
        homePos = transform.position;    // 记住出生点，游走不离开它太远
        PickNewDirection();

        // 自动找 Animator（根物体或子物体都行）
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        hasAnim = animator != null;
        if (hasAnim && !string.IsNullOrEmpty(speedParamName))
            speedParamHash = Animator.StringToHash(speedParamName);

        // 自动找/创建音源（2D音效，不受距离衰减）
        angrySource = GetComponent<AudioSource>();
        if (angrySource == null)
            angrySource = gameObject.AddComponent<AudioSource>();
        if (angrySource != null)
        {
            angrySource.playOnAwake = false;
            angrySource.spatialBlend = 0f;
        }
    }

    // 三结果判定：0=出片 1=普通 2=生气（三个概率各自可调，按占比掷骰）
    public int RollOutcome()
    {
        float total = goodShotChance + normalChance + angryChance;
        float r = Random.Range(0f, total);
        if (r < goodShotChance) return 0;   // 出片
        r -= goodShotChance;
        if (r < normalChance) return 1;     // 普通
        return 2;                           // 生气
    }

    // 根据结果返回金币（生气返回0）
    public int GetReward(int outcome)
    {
        if (outcome == 0) return goodReward;
        if (outcome == 1) return normalReward;
        return 0;
    }

    // 触发生气：变红 + 停止游走 + 延迟后追击
    public void TriggerAngry()
    {
        isAngry = true;
        canChase = false;   // 重置，等延迟结束才追击
        StopWander();       // 生气后先站在原地，不再游走
                            // 变红（兼容URP材质）Alpha=0.5 → 50%透明度
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Color red = new Color(1f, 0.2f, 0.2f, angryAlpha); // 透明度用Inspector的 Angry Alpha 参数
        foreach (Renderer r in renderers)
        {
            Material m = r.material;
            // 设置渲染为透明模式，保证Alpha生效
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

            if (m.HasProperty("_Color"))
                m.SetColor("_Color", red);
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", red);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", red * 1.2f);
            }
        }
        // 延迟后开始追击
        StartCoroutine(DelayedChase());
    }


    // 冻结：停止一切移动（淡出、游戏结束时由主控脚本调用）
    public void Freeze()
    {
        frozen = true;
    }

    void StopWander()
    {
        isMoving = false;
        wanderDir = Vector3.zero;
    }

    // 延迟几秒后允许追击
    IEnumerator DelayedChase()
    {
        yield return new WaitForSeconds(angryDelay);
        canChase = true;   // 延迟结束，开始追
    }

    void Update()
    {
        if (frozen)
        {
            SetAnimSpeed(0f);   // 冻结：回待机动画
            return;
        }

        if (isAngry)
        {
            // 延迟结束前原地不动（待机动画）；结束后追击（跑步动画）
            if (canChase && player != null)
            {
                Vector3 dir = player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    transform.position += dir * chaseSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
                    SetAnimSpeed(chaseSpeed);
                    return;
                }
            }
            SetAnimSpeed(0f);
            return;
        }

        // 正常状态：在出生点附近随机游走
        Wander();
    }

    // 随机游走：走一段→可能发呆→换方向，超出范围拉回出生点
    void Wander()
    {
        // 发呆阶段：原地停一会儿（待机动画）
        if (!isMoving)
        {
            changeTimer -= Time.deltaTime;
            if (changeTimer <= 0f) PickNewDirection();
            SetAnimSpeed(0f);
            return;
        }

        Vector3 toHome = homePos - transform.position;
        toHome.y = 0f;
        float distFromHome = toHome.magnitude;

        // 超出活动范围：朝出生点拉回
        Vector3 moveDir = wanderDir;
        if (distFromHome > wanderRadius)
            moveDir = toHome.normalized;

        transform.position += moveDir * wanderSpeed * Time.deltaTime;

        // 面朝移动方向
        if (moveDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 5f);

        changeTimer -= Time.deltaTime;
        if (changeTimer <= 0f) PickNewDirection();

        SetAnimSpeed(wanderSpeed);   // 游走 → 走路动画
    }

    // 随机选一个新方向（有概率发呆一会儿）
    void PickNewDirection()
    {
        changeTimer = changeInterval + Random.Range(-0.5f, 0.5f);
        if (Random.value < idleChance)
        {
            isMoving = false;
            wanderDir = Vector3.zero;
        }
        else
        {
            isMoving = true;
            float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            wanderDir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
    }

    // 把当前实际速度换算成动画 Speed 参数（0=待机，0.1~0.6=走，>0.6=跑）
    void SetAnimSpeed(float actualSpeed)
    {
        if (!hasAnim) return;
        animator.SetFloat(speedParamHash, actualSpeed * animSpeedScale);
    }

    // 生气音效（按男女模型选对应音效）
    void PlayAngrySound()
    {
        if (angrySource == null) return;
        AudioClip clip = isFemale ? angryFemaleClip : angryMaleClip;
        if (clip != null)
            angrySource.PlayOneShot(clip);
    }

    void OnCollisionEnter(Collision collision)
    {
        // 只有追击状态撞到玩家才扣血，延迟阶段撞到不扣血
        if (isAngry && canChase && collision.collider.CompareTag("Player"))
        {
            if (game != null)
                game.TakeDamage(damageOnHit);
            Destroy(gameObject);
        }
    }
}
