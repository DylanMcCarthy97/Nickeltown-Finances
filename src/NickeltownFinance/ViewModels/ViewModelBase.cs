using CommunityToolkit.Mvvm.ComponentModel;

namespace NickeltownFinance.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _loadingMessage = "On track…";
    private string? _errorMessage;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string LoadingMessage
    {
        get => _loadingMessage;
        set => SetProperty(ref _loadingMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    protected void BeginBusy(string message)
    {
        LoadingMessage = message;
        IsBusy = true;
    }

    protected void EndBusy()
    {
        IsBusy = false;
        LoadingMessage = "On track…";
    }
}
