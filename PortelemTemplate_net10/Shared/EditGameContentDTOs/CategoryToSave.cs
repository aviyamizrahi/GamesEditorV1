namespace AuthTemplate.Shared.EditGameContentDTOs;

public class CategoryToSave
{
    public int ID { get; set; }
    public int GameID { get; set; }
    public string Content { get; set; }
    public bool IsImage { get; set; }
    public List<ItemToSave> Items { get; set; }// רשימה שתכיל את כל הפריטים השייכים לקטגוריה הספציפית 

}