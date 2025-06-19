namespace AvitoLike.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? ParentId { get; set; }
    public string? Icon { get; set; }

    public Category? ParentCategory { get; set; }
    public List<Category> ChildCategories { get; set; } = new();
    public List<Ad> Ads { get; set; } = new();
}