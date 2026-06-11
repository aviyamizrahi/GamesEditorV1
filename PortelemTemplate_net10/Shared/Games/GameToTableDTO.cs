namespace AuthTemplate.Shared.Games;

public class GameToTableDTO
{
    public int ID { get; set; }
    public string GameName { get; set; } 
    public bool CanPublish { get; set; }
    public bool IsPublish { get; set; }
    public int GameCode { get; set; }
    
}

