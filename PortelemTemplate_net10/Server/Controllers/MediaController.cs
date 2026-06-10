using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AuthTemplate.Server.Helpers;
using UsersManager.Server;

namespace AuthTemplate.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ServiceFilter(typeof(AuthCheck))]
    public class MediaController : ControllerBase
    {
        private readonly FilesManage _filesManage;

        public MediaController(FilesManage filesManage)
        {
            _filesManage = filesManage;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(int authUserId, [FromBody] string imageBase64)
        {
            if (authUserId <= 0) return Unauthorized();
        
            string fileName = await _filesManage.SaveFile(imageBase64, "png", "uploadedFiles");
            return Ok(fileName);
        }

        [HttpPost("deleteImages")]
        public async Task<IActionResult> DeleteImages(int authUserId, [FromBody] List<string> images)
        {
            if (authUserId <= 0) return Unauthorized();
        
            var countFalseTry = 0;
            foreach (string img in images)
            {
                if (_filesManage.DeleteFile(img, "uploadedFiles") == false)
                    countFalseTry++;
            }

            if (countFalseTry > 0)
                return BadRequest("problem with " + countFalseTry.ToString() + " images");

            return Ok("deleted");
        }
    }
}
