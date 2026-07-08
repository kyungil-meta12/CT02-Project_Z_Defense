using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DropItemGetLogText : PoolObject
{
    [HideInInspector] public TextMeshProUGUI text;
    private float time;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        time += Time.deltaTime; // 2초 후에 스스로 풀에 반환
        if(time >= 2f)
        {
            ReturnInstance();
        }
    }

    public override void OnSpawn()
    {
        time = 0f;
    }
}
