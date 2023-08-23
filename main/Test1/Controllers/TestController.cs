using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Test1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {

        [Route("/")]
        [HttpGet]
        public ActionResult Index()
        {
            return Ok("This is the home for test 1");
        }
    }
}
