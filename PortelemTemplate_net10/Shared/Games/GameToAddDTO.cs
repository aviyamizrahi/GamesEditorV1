using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Games;

public class GameToAddDTO
{
    public int ID { get; set; }
    
    [Required(ErrorMessage = "שדה חובה")]
    [RegularExpression(@".*\S.*", ErrorMessage = "השדה לא יכול להכיל רק רווחים")]
    // חקירה עצמית על בדיקת תווים ריקים
    [StringLength(40, MinimumLength = 1, ErrorMessage = "יש להזין בין 1 ל-40 תווים")]
    public string GameName { get; set; }

    [MinLength(1, ErrorMessage = "יש להזין לפחות תו אחד")]
    [StringLength(40, ErrorMessage = "יש להזין עד 40 תווים")]
    public string Instructions { get; set; } = "מיינו את המכתב לקופסה הנכונה";
    
     public int? RoundTime { get; set; } = 30;
}