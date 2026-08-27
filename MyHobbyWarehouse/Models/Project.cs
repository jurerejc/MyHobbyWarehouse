namespace MyHobbyWarehouse.Models;

public class Project
{
    public int      Id          { get; set; }
    public string   Name        { get; set; } = string.Empty;
    public string   BoardName   { get; set; } = string.Empty;
    public string   Version { get; set; } = string.Empty;
    public string   Revision    { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public string   Notes       { get; set; } = string.Empty;
    public DateTime CreatedDate  { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    public bool     HasImage     { get; set; }

    public string DisplayName
    {
        get
        {
            var ver = $"{Version} {Revision}".Trim();
            return string.IsNullOrEmpty(ver) ? Name : $"{Name} ({ver})";
        }
    }

    public override string ToString() => DisplayName;

    public string DisplayDate => ModifiedDate.ToString("dd.MM.yyyy");
}
