using System.Collections.Generic;
using UnityEngine;

public class NormalZombieController : MonoBehaviour
{
    private Animator anim;
    private SphereCollider collider; // 시야 콜라이더
    private bool attackState = false;
    private List<GameObject> lookObj; // 시야 콜라이더 안에 들어온 오브젝트

    void Awake()
    {
        anim = GetComponent<Animator>();
        collider = GetComponent<SphereCollider>();
    }

    void Update()
    {
        if (attackState)
        {
            anim.Play("Zombie_Attack");
        }
        else
        {
            anim.Play("Zombie_Walk");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        
    }
}
