namespace KaylesStepsRecorder.Core.Enums;

[Flags]
public enum StepFlag
{
    None = 0,
    Important = 1,
    ExpectedResult = 2,
    ActualResult = 4,
    Bug = 8
}
