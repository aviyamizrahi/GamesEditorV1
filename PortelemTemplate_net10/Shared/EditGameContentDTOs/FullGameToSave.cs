
namespace AuthTemplate.Shared.EditGameContentDTOs;

public class FullGameToSave
// בלחיצה על שמירת שינויים בעמוד עריכת התוכן המודל הזה ישמש אותנו לשמירה כוללת של המשחק
// בתוך רשימת הקטגוריות יש
{
    public List<CategoryToSave> Categories { get; set; }
}