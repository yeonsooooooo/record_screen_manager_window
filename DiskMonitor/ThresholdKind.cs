namespace DiskMonitor;

public enum ThresholdKind
{
    /// <summary>사용률이 설정값(%) 이상이면 동작합니다.</summary>
    UsedPercent,

    /// <summary>남은 공간이 설정값(MB) 미만이면 동작합니다.</summary>
    FreeSpaceMegabytes,
}
