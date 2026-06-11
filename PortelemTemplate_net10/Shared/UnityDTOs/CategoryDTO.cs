namespace UsersManager.Shared;

public class CategoryDTO
{
    public int ID { get; set; }
    public string Content { get; set; }
    
    public bool IsImage { get; set; }
    public List<ItemsDTO> Items { get; set; }// רשימה שתכיל את כל הפריטים השייכים לקטגוריה הספציפית 
   
    // public int GameID { get; set; }
}