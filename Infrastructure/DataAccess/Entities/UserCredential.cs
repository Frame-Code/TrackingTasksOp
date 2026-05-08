namespace Infrastructure.DataAccess.Entities;

public abstract class UserCredential
{
    public string UserId { get; set; } = null!;
    public ApplicationUser ApplicationUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}