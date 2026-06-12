using System.Collections;
using UnityEngine;

public class ItemDropTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private ItemDropper dropper;
    public RewardCurrencyType dropType;

    void Awake()
    {
        dropper = GetComponent<ItemDropper>();
    }

    void Start()
    {
        StartCoroutine(TestCoroutine());
    }

    IEnumerator TestCoroutine()
    {
        yield return new WaitForSeconds(3f);
        dropper.CreateDropItem(transform.position, dropType, 10);
    }
}
