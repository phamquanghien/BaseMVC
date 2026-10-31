namespace BaseMVC.Domain.Exams;

public enum ExamStatus
{
    NotStarted,
    RegistrationOpen,
    RegistrationCompleted,
    AwaitingSchedule,
    ScheduleArranged,
    Taken,
    ResultsEntered,
    Completed
}
