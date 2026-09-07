using UnityEngine;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("NPC预制体（多种）")]
    public GameObject[] npcPrefabs;   // ← 改成数组：放4种不同NPC

    [Header("生成区域")]
    public string zoneTag = "SpawnZone";

    [Header("刷新规则")]
    public float respawnDelay = 1.5f;
    public int initialBatch = 1;

    [Header("结束控制")]
    public bool spawning = true;

    private List<Transform> zones = new List<Transform>();
    private Dictionary<Transform, float> respawnTimers = new Dictionary<Transform, float>();

    void Start()
    {
        GameObject[] zoneObjs = GameObject.FindGameObjectsWithTag(zoneTag);
        foreach (GameObject z in zoneObjs)
        {
            zones.Add(z.transform);
            respawnTimers[z.transform] = 0f;
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

        // ← 新增：随机挑一种NPC
        GameObject prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];

        Collider col = zone.GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;
        float x = Random.Range(b.min.x, b.max.x);
        float z = Random.Range(b.min.z, b.max.z);
        float y = b.min.y;

        Instantiate(prefab, new Vector3(x, y, z), Quaternion.identity, zone);
    }
}
