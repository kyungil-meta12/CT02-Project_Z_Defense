using UnityEngine;

public class MainMenuUI : TouchBackHandler
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnTouchBackAction += () =>
        {
            gameObject.SetActive(false);
            UIManager.Inst.RevertGameUI();
        };
    }

    // Update is called once per frame
    void Update()
    {
        UpdateTouchBackHandler();
    }
}
