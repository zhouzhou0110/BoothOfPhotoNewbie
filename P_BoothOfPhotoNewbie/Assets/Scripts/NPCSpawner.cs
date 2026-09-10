using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("NPC预制体（几种NPC）")]
    public GameObject[] npcPrefabs;

    [Header("各NPC刷新权重（与预制体一一对应）")]
    [Tooltip("权重可填整数或小数，例如游客5、上班族1。数组未填或全为0时退化为等概率1/N")]
    public float[] spawnWeights = new float[0];

    [Header("刷新区域")]
    public string zoneTag = "SpawnZone";

    [Header("刷新规则")]
    public float respawnDelay = 1.5f;
    public int initialBatch = 1;

    [Header("总开关")]
    public bool spawning = true;

    [Header("生成保护（防卡脚）")]
    [Tooltip("刷出的NPC与玩家的最小距离，避免直接刷在玩家身上")]
    public float minSpawnDistanceToPlayer = 2.5f;
    [Tooltip("寻找安全刷新点的最大尝试次数")]
    public int maxSpawnAttempts = 10;
    [Tooltip("NPC生成后碰撞器禁用时长（秒），0=不启用")]
    public float spawnColliderDelay = 0.4f;

    private List<Transform> zones = new List<Transform>();
    private Dictionary<Transform, float> respawnTimers = new Dictionary<Transform, float>();
    private Transform playerTransform;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) playerTransform = playerObj.transform;

        GameObject[] zoneObjs = GameObject.FindGameObjectsWithTag(zoneTag);
        foreach (GameObject z in zoneObjs)
        {
            zones.Add(z.transform);
            respawnTimers[z.transform] = 0f;

            // 关键：把刷新区域碰撞器强制设为触发器，避免隐形盒体卡住玩家
            Collider zc = z.GetComponent<Collider>();
            if (zc != null && !zc.isTrigger)
                zc.isTrigger = true;
        }

        foreach (Transform z in zones)
        {
            for (int i = 0; i < initialBatch; i++)
                SpawnNPCInZone(z);
        }
    }

    void Update()
    {
        if (!spawning) return;

        foreach (Transform zone in zones)
        {
            if (zone.childCount > 0) continue;

            respawnTimers[zone] += Time.deltaTime;
            if (respawnTimers[zone] >= respawnDelay)
            {
                respawnTimers[zone] = 0f;
                SpawnNPCInZone(zone);
            }
        }
    }

    void SpawnNPCInZone(Transform zone)
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0) return;

        // 按权重随机挑一个NPC
        GameObject prefab = PickRandomPrefab();
        if (prefab == null) return;

        Collider col = zone.GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;

        // 找刷新点：避开玩家（最多尝试 maxSpawnAttempts 次，失败则退回随机点）
        float x = 0f, z = 0f;
        bool foundSafe = false;
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            x = Random.Range(b.min.x, b.max.x);
            z = Random.Range(b.min.z, b.max.z);
            if (playerTransform == null)
            {
                foundSafe = true;
                break;
            }
            Vector3 candidate = new Vector3(x, b.min.y, z);
            if (Vector3.Distance(candidate, playerTransform.position) >= minSpawnDistanceToPlayer)
            {
                foundSafe = true;
                break;
            }
        }
        if (!foundSafe)
        {
            x = Random.Range(b.min.x, b.max.x);
            z = Random.Range(b.min.z, b.max.z);
        }
        float y = b.min.y;

        GameObject npc = Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity, zone);

        // 生成保护：短暂禁用NPC碰撞器，避免刷在玩家身上时卡住
        if (spawnColliderDelay > 0f)
            StartCoroutine(SpawnProtectionRoutine(npc, spawnColliderDelay));
    }

    // 生成后先禁用非触发器碰撞器，delay秒后再恢复
    IEnumerator SpawnProtectionRoutine(GameObject npc, float delay)
    {
        Collider[] cols = npc.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in cols)
            if (c != null && !c.isTrigger) c.enabled = false;
        yield return new WaitForSeconds(delay);
        foreach (Collider c in cols)
            if (c != null) c.enabled = true;
    }

    // 权重随机选择：spawnWeights 与 npcPrefabs 等长且总和>0时按权重抽；
    // 否则退化为等概率
    GameObject PickRandomPrefab()
    {
        if (spawnWeights != null && spawnWeights.Length == npcPrefabs.Length)
        {
            float total = 0f;
            for (int i = 0; i < spawnWeights.Length; i++)
                total += Mathf.Max(0f, spawnWeights[i]);
            if (total > 0f)
            {
                float r = Random.Range(0f, total);
                float acc = 0f;
                for (int i = 0; i < spawnWeights.Length; i++)
                {
                    acc += Mathf.Max(0f, spawnWeights[i]);
                    if (r < acc)
                        return npcPrefabs[i];
                }
            }
        }
        return npcPrefabs[Random.Range(0, npcPrefabs.Length)];
    }
}
