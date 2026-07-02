/// <summary>
/// SaveManager에 저장/불러오기를 위임할 수 있는 시스템이 구현하는 인터페이스.<para/>
/// 재화, 웨이브, 터렛 등 각 시스템은 자신의 데이터 형식만 책임지고,
/// 저장 시점 최적화와 파일 IO는 SaveManager가 전담한다.
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// 저장 파일 안에서 이 시스템의 데이터 구간을 구분하는 고유 키.
    /// </summary>
    string SaveKey { get; }

    /// <summary>
    /// 현재 상태를 JSON 문자열로 직렬화해 반환한다. 저장할 데이터가 없으면 null을 반환한다.
    /// </summary>
    string CaptureSaveData();

    /// <summary>
    /// 저장 파일에서 읽어온 JSON 문자열로 상태를 복원한다.
    /// </summary>
    void RestoreSaveData(string json);
}
