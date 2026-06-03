namespace MyHobbyWarehouse.Models;

public class Location
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasImage { get; set; }
}
