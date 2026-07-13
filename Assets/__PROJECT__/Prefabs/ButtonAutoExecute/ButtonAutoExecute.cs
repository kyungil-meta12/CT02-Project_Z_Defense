using System;
using UnityEngine;


/// <summary>
/// 버튼을 길게 누르고 있으면 자동으로 일정 간격마다 콜벡을 호출하는 클래스
/// </summary>
public class ButtonAutoExecute
{
    private bool PressState = false;

    private float AutoEnterTime = 0f;
    private float ExecuteInterval = 0f;
    private Action ExecuteAction;

    private bool AutoExecuteState = false;
    private float AutoEnterAccumTime = 0f;
    private float AutoExecuteAccumTime = 0f;

    public void SetExecuteEnterTime(float sec)
    {
        AutoEnterTime = sec;
    }

    public void SetExecuteInterval(float sec)
    {
        ExecuteInterval = sec;
    }

    public void RegisterAction(Action action)
    {
        ExecuteAction = action;
    }

    public void SetPressState(bool Flag)
    {
        PressState = Flag;
    }

    public bool GetAutoExecuteState()
    {
        return AutoExecuteState;
    }

    public void Update()
    {
        if(PressState)
        {
            AutoEnterAccumTime += Time.deltaTime;
            if(AutoEnterAccumTime >= AutoEnterTime)
            {
                if(!AutoExecuteState)
                {
                    AutoExecuteState = true;
                    AutoExecuteAccumTime = ExecuteInterval;
                }
            }
            if(AutoExecuteState)
            {
                AutoExecuteAccumTime += Time.deltaTime;
                if(AutoExecuteAccumTime >= ExecuteInterval)
                {
                    AutoExecuteAccumTime -= ExecuteInterval;
                    ExecuteAction?.Invoke();
                }
            }
        }
        else
        {
            AutoEnterAccumTime = 0f;
            AutoExecuteAccumTime = 0f;
            AutoExecuteState = false;
        }
    }
}