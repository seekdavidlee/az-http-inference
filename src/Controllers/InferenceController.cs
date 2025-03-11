using HttpInference.Models;
using HttpInference.Services;
using Microsoft.AspNetCore.Mvc;

namespace HttpInference.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InferenceController(Inference inference, ILogger<InferenceController> logger) : ControllerBase
    {
        [HttpPost("models/{key}")]
        public async Task<IActionResult> Post(Query query, string key)
        {
            logger.LogInformation("Key: {key}", key);

            var result = await inference.QueryAsync(key, query);
            if (result.Success)
            {
                return Ok(result.Item);
            }

            if (result.ErrorMessage is not null)
            {
                logger.LogError(result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }

            logger.LogError("unknown error has occurred");
            return new StatusCodeResult(500);
        }

        [HttpGet("models")]
        public IActionResult Get()
        {
            return Ok(inference.GetKeys());
        }
    }
}
