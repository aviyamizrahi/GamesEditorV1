using AuthTemplate.Shared.CheckDTOs;
using AuthTemplate.Shared.EditGameContentDTOs;
using AuthTemplate.Shared.Games;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UsersManager.Server;
using UsersManager.Shared;
using AuthTemplate.Server.Helpers;
using FilesManage = AuthTemplate.Server.Helpers.FilesManage;

namespace AuthTemplate.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ServiceFilter(typeof(AuthCheck))] //בדיקה שהמשתמש מחובר
    // מחזיר את המספר id של המשתמש אוטומטית. נצטרך אותו בשביל לשלוף את המשחקים של אותו משתמש

    public class GamesController : ControllerBase
    {
        private readonly DbRepository _db; // משתנה פרטי שנגיש רק מתוך המחלקה ושלא צריך לעדכן אותו בכלל 
        private readonly FilesManage _filesManage; // למחיקת התמונות בזמן מחיקת משחק


        public GamesController(DbRepository db, FilesManage filesManage) // הוספנו פרמטר
        {
            // כדי לא לפגוע במקור, שמירה של עותק פרטי, כל אחד יעבוד עם עותק משלו, למנוע מצב של דריס
            _db = db;
            _filesManage = filesManage; 
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
        
        [HttpDelete("DeleteGame/{gameId}")] // מטרת הפונקציה: מחיקת משחק
        public async Task<IActionResult> DeleteGame(int authUserId, int gameId)
            // פונקציית מחיקת משחק
        {
            if (authUserId <= 0) // בדיקה שזה מספר תקין
            {
                return Unauthorized("user is not authenticated");
            }
            object deleteParam = new
            {
                ID = gameId,
                UserId = authUserId
            };

            // שליפת שמות תמונות של פריטים לפני המחיקה
            string allImagesQuery = "SELECT Content FROM Items WHERE IsImage = true AND CategoryID IN (SELECT ID FROM Categories WHERE GameID = @ID) UNION SELECT Content FROM Categories WHERE IsImage = true AND GameID = @ID";
            var allImages = await _db.GetRecordsAsync<string>(allImagesQuery, new { ID = gameId });
            
            string deleteQuery = "DELETE FROM Games WHERE ID = @ID AND UserId = @UserId";
            int isDeleted = await _db.SaveDataAsync(deleteQuery, deleteParam);
                if (isDeleted == 1)
                {
                    // מחיקת התמונות מהתיקייה
                    foreach (string img in allImages)
                    {
                        _filesManage.DeleteFile(img, "uploadedFiles");
                    }
                    return Ok();
                }
            else
            {
                return BadRequest("שגיאה במחיקת משחק");
            }
        }
        
        
        [HttpGet("GetGameToDelete/{gameId}")] // אחראית על שליפת פרטי משחק שעומד בתנאי פרסום
        public async Task<IActionResult> GetGameToDelete(int authUserId, int gameId)
        {
            if (authUserId <= 0)
            {
                return Unauthorized("user is not authenticated");
            }
            
            object param = new { ID = gameId, UserId = authUserId };
            // וידוא שהמשחק שייך למשתמש ושליפת פרטיו
            string gameQuery = "SELECT ID, GameName FROM Games WHERE ID = @ID AND UserId = @UserId AND (CanPublish = true OR IsPublish = true)"; // נשלוף רק משחק שעומד בתנאי הפרסום או מפורסם
            var gameRecords = await _db.GetRecordsAsync<GameToDeleteDTO>(gameQuery, param);
            GameToDeleteDTO game = gameRecords.FirstOrDefault();
    
            if (game == null)
                return BadRequest("משחק לא נמצא");
    
            // ספירת קטגוריות
            string catQuery = "SELECT COUNT(*) FROM Categories WHERE GameID = @ID";
            var catRecords = await _db.GetRecordsAsync<int>(catQuery, param);
            game.CategoriesCount = catRecords.FirstOrDefault();
    
            // ספירת פריטים
            string itemQuery = "SELECT COUNT(*) FROM Items WHERE CategoryID IN (SELECT ID FROM Categories WHERE GameID = @ID)";
            var itemRecords = await _db.GetRecordsAsync<int>(itemQuery, param);
            game.ItemsCount = itemRecords.FirstOrDefault();
    
            return Ok(game); // נחזיר את הפרטים המעודכנים
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
        
        [HttpPost("CheckCanPublish/{gameId}")] 
        // המטרה: לבדוק אם ניתן לפרסם את המשחק בעזרת הפונקציית עזר, נוכל להשתמש בצד לקוח
        public async Task<IActionResult> CheckCanPublish(int gameId)
        {
            bool canPublish = await CanPublishFunc(gameId);
            return Ok(canPublish);
        }
        
        [HttpPost("publishGame")]
        public async Task<IActionResult> publishGame(int authUserId, PublishGame game)
         // הפונקציה יכולה גם לקבל משחק מפורסם ולבטל את הפרסום שלו -במידה ותקבל IsPublished = false
        {
            if (authUserId > 0)
            {
                object param = new { UserId = authUserId, gameID = game.ID };
                string checkQuery = "SELECT GameName FROM Games WHERE UserId = @UserId and ID = @gameID";
                var checkRecords = await _db.GetRecordsAsync<string>(checkQuery, param);
                string gameName = checkRecords.FirstOrDefault();

                if (gameName != null)
                    // אם מצאנו משחק בבסיס הנתונים
                {
                    if (game.IsPublish) // אם נשלחה בקשה של פרסום - אם המשתנה הגיע חיובי זה אומר שהמשתמש רוצה לפרסם
                    {
                        bool canPublish = await CanPublishFunc(game.ID); // בדיקה שאכן ניתן לפרסם את המשחק בעזרת הפונקציית עזר
                        if (!canPublish) // אם לא ניתן לפרסם והמשחק לא עומד בתנאים
                        {
                            return BadRequest("This game cannot be published"); // לא ניתן לפרסם את המשחק
                        }
                    }

                    object updateParam = new { IsPublish = game.IsPublish, ID = game.ID }; 
                    string updateQuery = "UPDATE Games SET IsPublish = @IsPublish WHERE ID = @ID"; 
                    // שם בבסיס הנתונים את הנתון שהגיע מהמשתמש - אם הגיע משתנה חיובי הוא ישים אותו חיובי - אם הגיע שלילי ישים אותו שלילי
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