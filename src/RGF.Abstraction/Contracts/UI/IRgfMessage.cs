
namespace Recrovit.RecroGridFramework.Abstraction.Contracts.UI;

public interface IRgfFormValidationMessages
{
    void RemoveFieldErrorMessages(string alias);

    void AddFieldErrorMessage(string alias, string message, bool checkDuplicates = true);

    void AddFormErrorMessage(string message, bool checkDuplicates = true);
}