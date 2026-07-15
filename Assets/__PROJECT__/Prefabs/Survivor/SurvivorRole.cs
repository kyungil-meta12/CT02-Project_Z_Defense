using System;

/// <summary>
/// 생존자의 현재 역할을 정의한다.
/// </summary>
public enum SurvivorRole
{
    survivor,
    constructionWorker,
    engineer
}

/// <summary>
/// 직업 미배정 생존자가 불러오기 후 대기할 진행 단계를 정의한다.
/// </summary>
public enum SurvivorRestoreStage
{
    TreatmentPending,
    RoleSelectionPending
}

/// <summary>
/// 저장 파일에 기록되는 생존자 한 명의 역할과 진행 단계다.
/// </summary>
[Serializable]
public class SurvivorSaveEntry
{
    public SurvivorRole Role;
    public SurvivorRestoreStage RestoreStage;
}
