namespace AuthTemplate.Shared.Games;

public class GameToDeleteDTO
// כדי להציג בפופ אפ המחיקה את כל המידע הרלוונטי על משחק שעומד בתנאי פרסום / מפורסם נצטרך את המודל הזה 
{
    public int ID { get; set; }
    public string GameName { get; set; }
    public int CategoriesCount { get; set; }
    public int ItemsCount { get; set; }
}