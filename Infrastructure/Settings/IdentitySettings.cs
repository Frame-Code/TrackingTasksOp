namespace Infrastructure.Settings;

public class IdentitySettings
{
    public Password Password { get; set; } = null!;
    public Lockout Lockout { get; set; } = null!;
    public User User { get; set; } = null!;
}

public class Password
{
    public int RequiredLength { get; set; }
    public bool RequireDigit { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireNonAlphanumeric { get; set; }
}

public class Lockout
{
    public int MaxFailedAccessAttempts { get; set; }
    public TimeSpan DefaultLockoutTimeSpan { get; set; }
}

public class User
{
    public bool RequireUniqueEmail { get; set; }
}
