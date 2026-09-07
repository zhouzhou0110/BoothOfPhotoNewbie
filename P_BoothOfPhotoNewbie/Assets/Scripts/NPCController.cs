using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("出片结果")]
    public float goodShotChance = 0.4f;  // 出片率（0~1）
    public int goodReward = 15;          // 出片金币

    [Header("普通结果")]                  // ← 新增
    public float normalChance = 0.55f;   // 普通率
    public int normalReward = 10;        // 普通金币

    [Header("生气结果")]
    public float angryChance = 0.05f;    // 生气率
    public float chaseSpeed = 6f;        // 追击速度
    public int damageOnHit = 10;         // 追到扣血

    private bool isAngry = false;
    private Transform player;
    private PlayerSectorIndicator game;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        game = FindObjectOfType<PlayerSectorIndicator>();
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

    // 触发生气：变红 + 追击
    public void TriggerAngry()
    {
        isAngry = true;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Color red = new Color(1f, 0.2f, 0.2f, 1f);
        foreach (Renderer r in renderers)
        {
            Material m = r.material;
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
    }

    void Update()
    {
        // 生气状态：朝玩家追击
        if (isAngry && player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.position += dir.normalized * chaseSpeed * Time.deltaTime;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isAngry && collision.collider.CompareTag("Player"))
        {
            if (game != null)
                game.TakeDamage(damageOnHit);
            Destroy(gameObject);
        }
    }
}
