using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public class AuditLogService
    {
        private readonly MIUContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(MIUContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Fire-and-forget from the caller's perspective, but always awaited —
        // a failed audit write should never silently vanish, so this does NOT
        // swallow exceptions. If logging fails, the caller's SaveChanges will
        // fail too, and that's the correct behavior for an audit trail:
        // "nothing financial/academic should happen without being logged" means
        // a broken log should block the action, not get quietly skipped.
        //
        // academicRegistrarId and deanLecturerId are NEW, optional, and default
        // to null — every existing Bursar call site (bursarId: ...) keeps
        // working exactly as before with zero changes required.
        public async Task LogAsync(
            string actionType,
            string description,
            int? bursarId = null,
            string? regNumber = null,
            string? entityType = null,
            int? entityId = null,
            int? academicRegistrarId = null,
            int? deanLecturerId = null)
        {
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            var entry = new AuditLog
            {
                Action = actionType,
                Details = description,
                BursarId = bursarId,
                RegNumber = regNumber,
                EntityType = entityType,
                EntityId = entityId,
                IPAddress = ipAddress,
                ActionDate = DateTime.Now,
                AcademicRegistrarId = academicRegistrarId,
                DeanLecturerId = deanLecturerId
            };

            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync();
        }
    }
}