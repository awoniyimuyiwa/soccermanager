using Application.Contracts;
using System.Diagnostics;

namespace Api.Services;

public class ActivityProvider(string appName) : IActivityProvider
{
    readonly ActivitySource _source = new(appName);

    public Activity? StartActivity(
        string name, 
        ActivityKind kind, 
        string? parentId)
        => _source.StartActivity(name, kind, parentId);


    // Ensures the ActivitySource is disposed when the DI container disposes the singleton
    public void Dispose() => _source.Dispose();
}


