namespace AuthTemplate.Shared.EditGameContentDTOs;

public class ItemToSave
{
    public int ID { get; set; } 
    public int CategoryID { get; set; }
    public string? Content { get; set; }
    public bool IsImage { get; set; }
}