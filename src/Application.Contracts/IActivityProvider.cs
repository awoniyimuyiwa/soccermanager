using System.Diagnostics;

namespace Application.Contracts;

public interface IActivityProvider
{
    Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        string? parentId = null);
}
