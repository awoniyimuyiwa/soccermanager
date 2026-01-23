using Domain;

namespace EntityFrameworkCore.Repositories;

class AuditLogRepository(ApplicationDbContext context) : BaseRepository<AuditLog>(context), IAuditLogRepository
{
}
