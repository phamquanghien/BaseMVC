namespace BaseMVC.Infrastructure.Auditing;

public enum AuditEventType
{
    Login,
    PasswordResetRequested,
    PasswordResetEmailDelivery,
    PasswordResetCompleted,
    PasswordChange,
    Logout,
    UserCreated,
    UserUpdated,
    UserDeleted,
    RoleCreated,
    RoleUpdated,
    RoleDeleted,
    RolePermissionsUpdated,
    ExamTypeCreated,
    ExamTypeUpdated,
    ExamTypeDeleted,
    ExamCreated,
    ExamUpdated,
    ExamDeleted,
    ExamStatusChanged,
    ExamCandidatesImported,
    ExamResultsImported
}
