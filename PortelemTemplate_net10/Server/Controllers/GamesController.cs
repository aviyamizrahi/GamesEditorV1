using AuthTemplate.Shared.CheckDTOs;
using AuthTemplate.Shared.EditGameContentDTOs;
using AuthTemplate.Shared.Games;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UsersManager.Server;
using UsersManager.Shared;

namespace AuthTemplate.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ServiceFilter(typeof(AuthCheck))] //בדיקה שהמשתמש מחובר
    // מחזיר את המספר id של המשתמש אוטומטית. נצטרך אותו בשביל לשלוף את המשחקים של אותו משתמש

    public class GamesController : ControllerBase
    {
        private readonly DbRepository _db; // משתנה פרטי שנגיש רק מתוך המחלקה ושלא צריך לעדכן אותו בכלל 

        public GamesController(DbRepository db)
        {
            _db = db; // כדי לא לפגוע במקור, שמירה של עותק פרטי, כל אחד יעבוד עם עותק משלו, למנוע מצב של דריסה
        }
        
        // GAMES // 

        [HttpGet("CheckGames")]
        public async Task<IActionResult> CheckGames(int authUserId)
        {
            if (authUserId < 0)
            {
                return BadRequest("לא זוהה מספר משתמש");
            }

            object gamesParam = new { userID = authUserId };
            string gamesQuery = "SELECT * FROM Games WHERE UserID = @userID GROUP BY ID";
            var records = await _db.GetRecordsAsync<GameToTableDTO>(gamesQuery, gamesParam);
            List<GameToTableDTO> games = new List<GameToTableDTO>();
            games = records.ToList();
            if (games.Count == 0)
            {
                return NotFound("לא נמצאו משחקים");
            }
            else
            {
                return Ok(games);
            }

        }

        [HttpPost("addGame")]
        public async Task<IActionResult> AddGames(int authUserId, GameToAddDTO gameToAdd)
        {
            //ניצור משחק חדש בבסיס הנתונים
            object newGameParam = new
            {
                UserId = authUserId,
                GameName = gameToAdd.GameName,
                Instructions = gameToAdd.Instructions,
                RoundTime = gameToAdd.RoundTime,
                GameCode = 0,
                IsPublish = false,
                CanPublish = false
            };
            string insertGameQuery =
                "INSERT INTO Games (UserID, GameName, Instructions, RoundTime, GameCode, IsPublish, CanPublish) VALUES (@UserId, @GameName, @Instructions, @RoundTime, @GameCode, @IsPublish, @CanPublish)";
            int newGameId = await _db.InsertReturnIdAsync(insertGameQuery, newGameParam);
            if (newGameId != 0)
            {
                int gameCode = newGameId + 1000;
                object updateParam = new
                {
                    ID = newGameId,
                    GameCode = gameCode
                };
                string updateCodeQuery = "UPDATE Games SET GameCode = @GameCode WHERE ID=@ID";
                int isUpdate = await _db.SaveDataAsync(updateCodeQuery, updateParam);
                if (isUpdate > 0)
                {
                    object param2 = new
                    {
                        ID = newGameId
                    };
                    string gameQuery = "SELECT ID, GameName, GameCode, IsPublish, CanPublish FROM Games WHERE ID = @ID";
                    var gameRecord = await _db.GetRecordsAsync<GameToTableDTO>(gameQuery, param2);
                    GameToTableDTO newGame = gameRecord.FirstOrDefault();
                    return Ok(newGame);
                }

                return BadRequest("Game code not created");
            }

            return BadRequest("Game not created");

        }

       
        
        // -------- PUBLISH FUNCS ---------
        private async Task<bool> CanPublishFunc(int gameId)
            // פונקציית עזר לבדיקה האם ניתן לפרסם
        {
            int minCategories = 3;
            int minItemsPerCategory = 6;
            bool canPublish = false;
            int isUpdate;

            object param = new { ID = gameId };
            string queryCategoriesCount = "SELECT ID FROM Categories WHERE GameID = @ID";
            var recordsCategoriesCount = await _db.GetRecordsAsync<CategoryDTO>(queryCategoriesCount, param);
            List<CategoryDTO> categories = recordsCategoriesCount.ToList();

            string updateQuery;
            if (categories.Count >= minCategories)
            {
                foreach (CategoryDTO category in categories)
                {
                    object itemParam = new { ID = category.ID, };
                    string queryItemsCount = "SELECT ID FROM Items WHERE CategoryID = @ID";
                    var recordsItemsCount = await _db.GetRecordsAsync<ItemsDTO>(queryItemsCount, itemParam);
                    category.Items = recordsItemsCount.ToList();
                    if (category.Items.Count < minItemsPerCategory)
                    {
                        canPublish = false;
                        updateQuery = "UPDATE Games SET IsPublish = false, CanPublish = false WHERE ID = @ID";
                        isUpdate = await _db.SaveDataAsync(updateQuery, param);
                        return canPublish;
                    }
                    
                }
                canPublish = true;
                updateQuery = "UPDATE Games SET CanPublish = true WHERE ID = @ID";
                isUpdate = await _db.SaveDataAsync(updateQuery, param);
                return canPublish;

            }
            canPublish = false;
            updateQuery = "UPDATE Games SET IsPublish = false, CanPublish = false WHERE ID = @ID";
            isUpdate = await _db.SaveDataAsync(updateQuery, param);
            return canPublish;
        }
        
        [HttpPost("publishGame")]
        public async Task<IActionResult> publishGame(int authUserId, PublishGame game)
        {
            if (authUserId > 0)
            {
                object param = new { UserId = authUserId, gameID = game.ID };
                string checkQuery = "SELECT GameName FROM Games WHERE UserId = @UserId and ID = @gameID";
                var checkRecords = await _db.GetRecordsAsync<string>(checkQuery, param);
                string gameName = checkRecords.FirstOrDefault();

                if (gameName != null)
                {
                    if (game.IsPublish)
                    {
                        bool canPublish = await CanPublishFunc(game.ID);
                        if (!canPublish)
                        {
                            return BadRequest("This game cannot be published");
                        }
                    }

                    object updateParam = new { IsPublish = game.IsPublish, ID = game.ID };
                    string updateQuery = "UPDATE Games SET IsPublish = @IsPublish WHERE ID = @ID";
                    int isUpdate = await _db.SaveDataAsync(updateQuery, updateParam);

                    if (isUpdate == 1)
                    {
                        return Ok();
                    }
                    return BadRequest("Update Failed");
                }
                return BadRequest("It's Not Your Game");
            }
            else
            {
                return Unauthorized("user is not authenticated");
            }
        }
    }

}
    

// //שיטה שבודקת אם ניתן לפרסם את המשחק
// //אם נמצא שלא ניתן לפרסם - נוודא שהמשחק גם לא מפורסם


// חשוב
// כל בן אדם שפונה לקונטרולר המשחקים צריך להיות מחובר - אם לא צריך לזרוק אותו לפורטלמ כדי להתחבר
// הקובץ aoutcheck בודק את התחברות המשתמש - עושה סט של בדיקות ואמור להגיד לקונטרולר ״כן המשתמש מאושר״ ואז המשתמש יוכל לעשות את כל הפעולות - חייב להופיע בכל קונטרולר שחייב הזדהות (ביוניטי לא צריך נגיד) אבל אם יהיה categoryController וכו. כל מה שנוגע בבסיס הנתונים. 
// השורות האלה מחזירות את היוזר id שנוכל לקלוט ישירות לתוך הפונקציות