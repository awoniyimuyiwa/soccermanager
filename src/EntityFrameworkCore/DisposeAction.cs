namespace EntityFrameworkCore;

public class DisposeAction(Action action) : IDisposable
{
    public void Dispose() => action();
}


