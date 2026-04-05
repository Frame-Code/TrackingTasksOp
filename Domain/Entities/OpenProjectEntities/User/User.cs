using System.Text.Json.Serialization;

namespace Domain.Entities.OpenProjectEntities.User;

public class User
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class UserCollection : CollectionBase
{
    [JsonPropertyName("_embedded")]
    public UserEmbedded Embedded { get; set; } = new UserEmbedded();
}

public class UserEmbedded
{
    [JsonPropertyName("elements")]
    public List<User> Elements { get; set; } = new List<User>();
}
