
namespace AuthTemplate.Shared.EditGameContentDTOs;

public class FullGameToSave
// בלחיצה על שמירת שינויים בעמוד עריכת התוכן המודל הזה ישמש אותנו לשמירה כוללת של המשחק
// בתוך רשימת הקטגוריות יש
{
    public List<CategoryToSave> Categories { get; set; }
    public List<int> DeletedCategoryIds { get; set; } 
    // כדי לדעת אילו קטגוריות יש למחוק - במידה והמשתמש מחק
    public List<int> DeletedItemIds { get; set; }
    // גם על הפריטים
}