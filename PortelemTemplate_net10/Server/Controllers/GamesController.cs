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

        [HttpGet] // אין לפונקציה הזאת באמת שימוש חוץ מלהראות את הid, לא צריך אותה
        public async Task<ActionResult<int>> GetLoginUser(int authUserId)
        {
            return Ok(authUserId);
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

        [HttpGet("GetGame/{gameId}")]
        public async Task<IActionResult> GetGame(int authUserId, int gameId)
        // פונקציית עריכת משחק
        {
            if (authUserId > 0)
            {
                object param = new
                {
                    UserId = authUserId,
                    ID = gameId
                };

                string query =
                    "SELECT ID, GameName, RoundTime, Instructions FROM Games WHERE ID = @ID AND UserId = @UserId";
                var records = await _db.GetRecordsAsync<GameToAddDTO>(query, param);
                GameToAddDTO game = records.FirstOrDefault();

                if (game != null)
                {
                    return Ok(game);
                }

                return BadRequest("Game not found");
            }

            return Unauthorized("user is not authenticated");
        }

        [HttpPost("UpdateGame")]
        public async Task<IActionResult> UpdateGame(int authUserId, GameToAddDTO game)
        {
            if (authUserId > 0)
            {
                object updateParam = new
                {
                    GameName = game.GameName,
                    RoundTime = game.RoundTime,
                    Instructions = game.Instructions,
                    ID = game.ID,
                    UserId = authUserId
                };

                string updateQuery =
                    "UPDATE Games SET GameName=@GameName, RoundTime=@RoundTime, Instructions=@Instructions WHERE ID=@ID AND UserId=@UserId";

                int isUpdated = await _db.SaveDataAsync(updateQuery, updateParam);

                if (isUpdated == 1)
                {
                    return Ok();

                }

                return BadRequest("It's Not Your Game");
            }

            return Unauthorized("user is not authenticated");
        }

        [HttpDelete("DeleteGame{gameId}")]
        public async Task<IActionResult> DeleteGame(int authUserId, int gameId)
        // פונקציית מחיקת משחק
        {
            if (authUserId > 0)
            {
                object deleteParam = new
                {
                    ID = gameId,
                    UserId = authUserId
                };

                string deleteQuery = "DELETE FROM Games WHERE ID = @ID";
                int isDeleted = await _db.SaveDataAsync(deleteQuery, deleteParam);
                if (isDeleted == 1)
                {
                    return Ok();
                }

            }

            return Unauthorized("user is not authenticated");
        }
        
        
        // EDIT GAME //
        
        [HttpGet("getGameContent/{id}")]
        public async Task<IActionResult> GetGameContent(int authUserId, int id)
        {
            if (authUserId <= 0) 
            {
                return Unauthorized("user is not authenticated");
            }
            // וידוא שהמשחק שייך למשתמש המחובר 
            object checkParam = new { UserId = authUserId, GameID = id };
            string checkQuery = "SELECT ID FROM Games WHERE ID = @GameID AND UserID = @UserId";
            var check = await _db.GetRecordsAsync<int>(checkQuery, checkParam);
            if (!check.Any())
                // אם המשחק לא שייך למשתמש תחזיר שגיאה
                return BadRequest("Game not found or not yours");

            // שליפת כל הקטגוריות של המשחק
            object param = new { GameID = id };
            string catQuery = "SELECT ID, GameID, Content, IsImage FROM Categories WHERE GameID = @GameID";
            var records = await _db.GetRecordsAsync<CategoryToSave>(catQuery, param);
            List<CategoryToSave> categories = records.ToList();

            foreach (var cat in categories)
            {
                object itemParam = new { CategoryID = cat.ID };
                string itemQuery = "SELECT ID, CategoryID, Content, IsImage FROM Items WHERE CategoryID = @CategoryID";
                var items = await _db.GetRecordsAsync<ItemToSave>(itemQuery, itemParam);
                cat.Items = items.ToList();
            }

           // 
           FullGameToSave fullGame = new FullGameToSave() { Categories = categories };
           // יצירת משתנה להחזרת המידע עם רשימת הקטגוריות שנשלפו

            return Ok(fullGame);
        }
        
        
        
        
        // CATEGORIES // 
        
        // [HttpGet("GetCategories/{gameId}")]
        // public async Task<IActionResult> GetCategories(int authUserId, int gameId)
        // // שליפה הקטגוריות של משחק קיים
        // {
        //     if (authUserId <= 0)
        //         return Unauthorized("user is not authenticated");
        //
        //     // שליפת קטגוריות לפי משחק
        //     object param = new { GameID = gameId };
        //     string catQuery = "SELECT ID, Content, IsImage FROM Categories WHERE GameID = @GameID";
        //     var catRecords = await _db.GetRecordsAsync<CategoryDTO>(catQuery, param);
        //     List<CategoryDTO> categories = catRecords.ToList();
        //
        //     if (categories.Count == 0)
        //     {
        //         return NotFound("אין קטגוריות למשחק");
        //     }
        //     // שליפת פריטים לכל קטגוריה
        //     foreach (CategoryDTO cat in categories)
        //     {
        //         object itemParam = new { CategoryID = cat.ID };
        //         string itemQuery = "SELECT ID, CategoryID, Content, IsImage FROM Items WHERE CategoryID = @CategoryID";
        //         var itemRecords = await _db.GetRecordsAsync<ItemsDTO>(itemQuery, itemParam);
        //         cat.Items = itemRecords.ToList();
        //     }
        //
        //     return Ok(categories);
        // }
        //
        // [HttpPost("SaveCategory")]
        // public async Task<IActionResult> SaveCategory(int authUserId, CategoryToSave category)
        // // פונקציה שמעדכנת קטגוריה קיימת או מוסיפה קטגוריה חדשה
        // {
        //     if (authUserId <= 0)
        //         return Unauthorized("user is not authenticated");
        //
        //     if (category.ID == 0) // קטגוריה חדשה
        //     {
        //         object param = new
        //         {
        //             GameID = category.GameID,
        //             Content = category.Content,
        //             IsImage = category.IsImage
        //         };
        //         string query = "INSERT INTO Categories (GameID, Content, IsImage) VALUES (@GameID, @Content, @IsImage)";
        //         int newId = await _db.InsertReturnIdAsync(query, param);
        //
        //         if (newId > 0)
        //         {
        //             category.ID = newId;
        //             return Ok(category);
        //         }
        //         return BadRequest("יצירת קטגוריה נכשלה");
        //     }
        //     else // קטגוריה קיימת
        //     {
        //         object param = new
        //         {
        //             ID = category.ID,
        //             Content = category.Content,
        //             IsImage = category.IsImage
        //         };
        //         string query = "UPDATE Categories SET Content = @Content, IsImage = @IsImage WHERE ID = @ID";
        //         int isUpdated = await _db.SaveDataAsync(query, param);
        //
        //         if (isUpdated > 0)
        //         {
        //             return Ok(category);
        //         }
        //             
        //     
        //         return BadRequest("Category not updated");
        //     }
        // }
        
        
        //////////// PUBLISH FUNCS ///////////
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