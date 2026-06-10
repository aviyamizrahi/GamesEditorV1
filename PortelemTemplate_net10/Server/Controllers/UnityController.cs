using Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UsersManager.Shared;

namespace AuthTemplate.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnityController : ControllerBase
    {
        private readonly DbRepository _db; // משתנה פרטי שנגיש רק מתוך המחלקה ושלא צריך לעדכן אותו בכלל 
        public UnityController(DbRepository db)
        {
            _db = db; // כדי לא לפגוע במקור, שמירה של עותק פרטי, כל אחד יעבוד עם עותק משלו, למנוע מצב של דריסה
        }

    [HttpGet("GetInfo/{gameCodeToCheck}")]
        public async Task<IActionResult> GetInformation(int gameCodeToCheck)
        {
            if (gameCodeToCheck > 0)
            {
                object gameCodeParam = new { GameCodeToCheck = gameCodeToCheck };
                string gameToCheckQuery =
                    "SELECT ID FROM Games WHERE GameCode=@GameCodeToCheck"; // בדיקה אם המשחק קיים
                var checkCode = await _db.GetRecordsAsync<int>(gameToCheckQuery, gameCodeParam);
                int? gameID = checkCode.FirstOrDefault(); // סימן שאלה מאפשר למספר להיות null
                if (gameID > 0)
                    // אם חזר לי קוד משחק תקין מהטבלה
                {
                    string fullGameQuery =
                        "SELECT GameName, Instructions, RoundTime, GameCode FROM Games WHERE GameCode= @GameCodeToCheck AND IsPublish = true";
                    // שליפה מלאה של המשחק במידה והוא פורסם. אין סיבה לשלוף גם id כי היוניטי לא צריך אותו
                    var fullGameRecord = await _db.GetRecordsAsync<GameDTO>(fullGameQuery, gameCodeParam);
                    GameDTO fullGame = fullGameRecord.FirstOrDefault();
                    if (fullGame != null)
                    {
                        object gameIdParam = new { GameID = gameID }; // פרמטר של מזהה המשחק
                        string categoriesQuery =
                            "SELECT ID, Content, IsImage FROM Categories WHERE GameID = @GameID GROUP BY ID"; // שאילתת שליפה של כל הקטגוריות
                        var catRecords =
                            await _db.GetRecordsAsync<CategoryDTO>(categoriesQuery,
                                gameIdParam); // שימוש בפרמטר הקיים 
                        fullGame.Categories =
                            catRecords.ToList(); // לשמור את הנתונים שהתקבלו בתוך רשימת הקטגוריות במופע שישלח ליוניטי
                        if (fullGame.Categories.Count > 2)
                        {
                            int itemsCount = 0;
                            foreach (CategoryDTO category in
                                     fullGame.Categories) // לכל אחת מהקטגוריות תמצא את רשימת הפריטים שלה
                            {
                                object itemParam = new { CategoryID = category.ID };
                                string itemQuery =
                                    "SELECT CategoryID, Content, IsImage FROM Items WHERE CategoryID = @CategoryID GROUP BY ID";
                                var ItemRecords = await _db.GetRecordsAsync<ItemsDTO>(itemQuery, itemParam);
                                category.Items = ItemRecords.ToList();
                            }

                            return Ok(fullGame);
                        }
                        return BadRequest("המשחק לא מכיל מספיק קטגוריות");
                        
                    }

                    return BadRequest("המשחק קיים אך לא מפורסם");
                }

                return BadRequest("קוד משחק לא נמצא");
            }

            return BadRequest("קוד משחק לא הוקלד נכון");

        }
    }
}
