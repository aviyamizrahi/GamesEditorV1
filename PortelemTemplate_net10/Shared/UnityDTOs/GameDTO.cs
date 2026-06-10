namespace UsersManager.Shared;

public class GameDTO 
    // מטרתו היא לקבל את כל פרטי המשחק הרלוונטים ליוניטי
{   
    public int ID { get; set; }
    public string GameName { get; set; } 
    public string Instructions { get; set; }
    public int RoundTime { get; set; }
    public List<CategoryDTO> Categories { get; set; }
}

// public bool CanPublish { get; set; }
     // public bool IsPublish { get; set; }
     // public int UserID { get; set; }
      // public int GameCode { get; set; }