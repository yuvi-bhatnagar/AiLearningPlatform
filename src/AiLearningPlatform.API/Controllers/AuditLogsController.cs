using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/[controller]")]
public class AuditLogsController : ControllerBase
{
      private readonly AppDbContext _db;

      public AuditLogsController(AppDbContext db)
      {
          _db = db;
      }

      [HttpGet]
      public async Task<IActionResult> GetAuditLogs()
      {
          var logs = await _db.AuditLogs
              .Include(al => al.User)
              .OrderByDescending(al => al.TimestampUtc)
              .Take(100)
              .Select(al => new {
                  al.Id,
                  al.Action,
                  al.Details,
                  al.IpAddress,
                  al.TimestampUtc,
                  Username = al.User != null ? al.User.Username : "Anonymous"
              })
              .ToListAsync();

          return Ok(logs);
      }
}
