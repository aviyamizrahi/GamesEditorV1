using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    public class EditGameController : ControllerBase
    {
        private readonly DbRepository _db; // משתנה פרטי שנגיש רק מתוך המחלקה ושלא צריך לעדכן אותו בכלל 

        public EditGameController(DbRepository db)
        {
            _db = db; // כדי לא לפגוע במקור, שמירה של עותק פרטי, כל אחד יעבוד עם עותק משלו, למנוע מצב של דריסה
        }
        
        // ----- GAME SETTING -----

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
        
        
        // ----- EDIT GAME ----- 
        
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

        [HttpPost("SaveContent/{gameId}")]
        // שמירת כל השינויים בתוכן המשחק בלחיצה אחת
        public async Task<IActionResult> SaveContent(int authUserId, int gameId, FullGameToSave fullGame)
        {
            if (authUserId <= 0)
            {
                return Unauthorized("user is not authenticated");
            }

            // וידוא שהמשחק שייך למשתמש המחובר
            object checkParam = new { UserId = authUserId, GameID = gameId };
            string checkQuery = "SELECT ID FROM Games WHERE ID = @GameID AND UserID = @UserId";
            var check = await _db.GetRecordsAsync<int>(checkQuery, checkParam);
            if (!check.Any())
            {
                return BadRequest("Game not found or not yours");
            }

            // מחיקת פריטים שנמחקו
            if (fullGame.DeletedItemIds != null)
            {
                foreach (int itemId in fullGame.DeletedItemIds)
                {
                    await DeleteItem(itemId);
                }
            }

            // מחיקת קטגוריות שנמחקו (הפריטים נמחקים אוטומטית עם CASCADE)
            if (fullGame.DeletedCategoryIds != null)
            {
                foreach (int catId in fullGame.DeletedCategoryIds)
                {
                    await DeleteCategory(catId);
                }
            }

            // שמירת קטגוריות ופריטים
            if (fullGame.Categories != null)
            {
                foreach (var cat in fullGame.Categories)
                {
                    // דלג על קטגוריות ריקות לחלוטין
                    if (string.IsNullOrEmpty(cat.Content))
                        continue;

                    if (cat.ID == 0)
                        // קטגוריה חדשה — הוסף לבסיס הנתונים
                        cat.ID = await AddCategory(cat, gameId);
                    else
                        // קטגוריה קיימת — עדכן
                        await UpdateCategory(cat);

                    // שמירת פריטים של הקטגוריה
                    if (cat.Items != null)
                    {
                        foreach (var item in cat.Items)
                        {
                            // דלג על פריטים ריקים לחלוטין
                            if (string.IsNullOrEmpty(item.Content))
                                continue;

                            item.CategoryID = cat.ID; // וידוא שהפריט משויך לקטגוריה הנכונה
                            if (item.ID == 0)
                                // פריט חדש — הוסף
                                await AddItem(item);
                            else
                                // פריט קיים — עדכן
                                await UpdateItem(item);
                        }
                    }
                }
            }

            return Ok();
        }

        // ─────────────────────────────────────────
        //        פונקציות עזר פרטיות — קטגוריות
        // ─────────────────────────────────────────

        private async Task<int> AddCategory(CategoryToSave cat, int gameId)
        // הוספת קטגוריה חדשה לבסיס הנתונים, מחזירה את ה-ID החדש
        {
            object param = new { GameID = gameId, Content = cat.Content, IsImage = cat.IsImage };
            string query = "INSERT INTO Categories (GameID, Content, IsImage) VALUES (@GameID, @Content, @IsImage)";
            int newId = await _db.InsertReturnIdAsync(query, param);
            return newId;
        }

        private async Task UpdateCategory(CategoryToSave cat)
        // עדכון קטגוריה קיימת
        {
            object param = new { ID = cat.ID, Content = cat.Content, IsImage = cat.IsImage };
            string query = "UPDATE Categories SET Content=@Content, IsImage=@IsImage WHERE ID=@ID";
            await _db.SaveDataAsync(query, param);
        }

        private async Task DeleteCategory(int catId)
        // מחיקת קטגוריה — הפריטים נמחקים אוטומטית עם CASCADE
        {
            object param = new { ID = catId };
            string query = "DELETE FROM Categories WHERE ID=@ID";
            await _db.SaveDataAsync(query, param);
        }

        // ─────────────────────────────────────────
        //        פונקציות עזר פרטיות — פריטים
        // ─────────────────────────────────────────

        private async Task AddItem(ItemToSave item)
        // הוספת פריט חדש לבסיס הנתונים
        {
            object param = new { CategoryID = item.CategoryID, Content = item.Content, IsImage = item.IsImage };
            string query = "INSERT INTO Items (CategoryID, Content, IsImage) VALUES (@CategoryID, @Content, @IsImage)";
            await _db.InsertReturnIdAsync(query, param);
        }

        private async Task UpdateItem(ItemToSave item)
        // עדכון פריט קיים
        {
            object param = new { ID = item.ID, Content = item.Content, IsImage = item.IsImage };
            string query = "UPDATE Items SET Content=@Content, IsImage=@IsImage WHERE ID=@ID";
            await _db.SaveDataAsync(query, param);
        }

        private async Task DeleteItem(int itemId)
        // מחיקת פריט בודד
        {
            object param = new { ID = itemId };
            string query = "DELETE FROM Items WHERE ID=@ID";
            await _db.SaveDataAsync(query, param);
        }
    }
}
