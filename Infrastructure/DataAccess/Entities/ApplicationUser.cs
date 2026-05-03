using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.DataAccess.Entities;

public class ApplicationUser : IdentityUser
{
    public int OpenProjectUserId { get; set; }
    public int OpenProjectInstanceId { get; set; }
    public OpenProjectInstance OpenProjectInstance { get; set; } = null!;
    public AuthMethod AuthMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}