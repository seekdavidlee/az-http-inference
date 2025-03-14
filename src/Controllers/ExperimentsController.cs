using HttpInference.Models;
using HttpInference.Services;
using Microsoft.AspNetCore.Mvc;

namespace HttpInference.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ExperimentsController(ExperimentQueue experimentQueue, ILogger<ExperimentsController> logger) : ControllerBase
    {
        [HttpPost("images")]
        public IActionResult HandleImages(ExperimentRun run)
        {
            if (run.Id != Guid.Empty)
            {
                return BadRequest("Id must not be set");
            }

            if (run.Iterations < 1)
            {
                return BadRequest("Iterations must be set");
            }

            if (run.SystemPrompt is null)
            {
                return BadRequest("SystemPrompt must be set");
            }

            if (run.Temperature < 0 || run.Temperature > 1)
            {
                return BadRequest("Temperature must be between 0 and 1");
            }

            if (run.TopP < 0 || run.TopP > 1)
            {
                return BadRequest("TopP must be between 0 and 1");
            }

            if (run.MaxTokens < 1)
            {
                return BadRequest("MaxTokens must be set");
            }

            run.Id = Guid.NewGuid();

            logger.LogInformation("Received image experiment request: experiment id: {experiment_id}, project id {project_id}, model id {model_id}",
                run.ExperimentId,
                run.ProjectId,
                run.ModelId);

            experimentQueue.AddImageExperiment(run);
            return Ok();
        }
    }
}
