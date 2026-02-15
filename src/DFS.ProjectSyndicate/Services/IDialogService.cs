namespace DFS.ProjectSyndicate.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        bool ShowConfirmation(string message, string title);
    }
}
