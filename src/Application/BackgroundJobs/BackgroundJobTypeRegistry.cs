using Application.Contracts.BackgroundJobs;
using Domain.BackgroundJobs;

namespace Application.BackgroundJobs;

class BackgroundJobTypeRegistry(Dictionary<Type, BackgroundJobType> mappings) : IBackgroundJobTypeRegistry
{
    readonly IReadOnlyDictionary<Type, BackgroundJobType> _mappings = mappings;

    public BackgroundJobType GetType(BackgroundJobHandlerDto dto) =>
        _mappings.TryGetValue(dto.GetType(), out var type)
            ? type
            : throw new NotSupportedException($"No mapping for DTO: {dto.GetType().Name}");
}

