namespace NickeltownFinance.Services;

public interface INavigationService
{
    event Action<Type>? Navigated;

    void Navigate<T>() where T : class;

    void Navigate(Type viewModelType);
}

public class NavigationService : INavigationService
{
    public event Action<Type>? Navigated;

    public void Navigate<T>() where T : class => Navigate(typeof(T));

    public void Navigate(Type viewModelType) => Navigated?.Invoke(viewModelType);
}
