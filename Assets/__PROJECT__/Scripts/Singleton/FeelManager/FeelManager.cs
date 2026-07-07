using MoreMountains.Feedbacks;
using UnityEngine;

public class FeelManager : MonoBehaviour
{
    public static FeelManager Inst;
    public MMFeedbacks attackFeedback;
    public MMFeedbacks tankSkillFeedback;
    public MMFeedbacks boomerSkillFeedback;
    public MMFeedbacks screamerSkillFeedback;
    public MMFeedbacks obstacleBrokenFeedback;

    private void Awake()
    {
        if(Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
    }
}