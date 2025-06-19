namespace AvitoLike.Models;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public List<UserRole> UserRoles { get; set; } = new();
}