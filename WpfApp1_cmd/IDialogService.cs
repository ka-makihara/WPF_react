namespace WpfApp1_cmd
{
    public interface IDialogService
    {
        Task<bool> Question(string message);
    }
}
