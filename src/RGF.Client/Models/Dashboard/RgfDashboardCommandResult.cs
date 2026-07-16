namespace Recrovit.RecroGridFramework.Client.Models.Dashboard;

public sealed class RgfDashboardCommandResult
{
    public RgfDashboardCommandResult(
        bool succeeded,
        RgfDashboardValidationResult? validationResult = null,
        string? failureCode = null,
        string? errorMessage = null)
    {
        Succeeded = succeeded;
        ValidationResult = validationResult ?? new();
        FailureCode = failureCode;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public RgfDashboardValidationResult ValidationResult { get; }

    public string? FailureCode { get; }

    public string? ErrorMessage { get; }

    public static RgfDashboardCommandResult Success(RgfDashboardValidationResult? validationResult = null)
        => new(true, validationResult);

    public static RgfDashboardCommandResult Failure(
        string failureCode,
        string? errorMessage,
        RgfDashboardValidationResult? validationResult = null)
        => new(false, validationResult, failureCode, errorMessage);
}
