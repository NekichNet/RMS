using Business.Logging;
using Business.Models;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Business.Controllers
{
	[Route("api/v1/[controller]")]
	[ApiController]
	public class DocController : ControllerBase
	{
		private readonly IWebHostEnvironment _environment;
		private readonly ILogger<DocController> _logger;
        private readonly DocService _docService;

        public DocController(IWebHostEnvironment environment, ILogger<DocController> logger, DocService docService)
        {
            _environment = environment;
            _logger = logger;
            _docService = docService;
        }

		[HttpPost]
		[Authorize]
		public async Task<IActionResult> CreateDoc([FromForm] NewDocDto dto)
		{
			try
			{
				if (dto.File == null || dto.File.Length == 0)
				{
					return BadRequest(new { error = "Файл не передан или пуст." });
				}

				_docService.Actor = HttpContext.Items["Actor"] as Unit;

				var action = await _docService.CreateAsync(dto.Title);
				if (!action.IsSuccess)
				{
					return BadRequest(new { error = action.Message });
				}

				string folderName = action.Value.GetFilesFolderName();

				_logger.LogInformation(
					EventIds.Saving, $"Начинается сохранение файла документа {action.Value.Title} в {folderName} с ID {action.Value.Id}");

				var targetFolder = Path.Combine(_environment.WebRootPath, folderName);
				if (!Directory.Exists(targetFolder))
					Directory.CreateDirectory(targetFolder);

				var directoryInfo = new DirectoryInfo(targetFolder);
				var existingFiles = directoryInfo.GetFiles($"{action.Value.Id}.*");
				foreach (var existingFile in existingFiles)
				{
					existingFile.Delete();
				}

				var extension = Path.GetExtension(dto.File.FileName).ToLowerSuffix();
				var fileName = $"{action.Value.Id}{extension}";
				var fullPath = Path.Combine(targetFolder, fileName);

				using (var stream = new FileStream(fullPath, FileMode.Create))
				{
					await dto.File.CopyToAsync(stream);
				}

				_logger.LogInformation(EventIds.Ok, $"Файл документа '{action.Value.Title}' с ID {action.Value.Id} сохранён в {folderName} как {fileName}");

				return Ok(action.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in CreateDoc: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetAllDocs()
        {
            try
            {
				var action = await _docService.GetAllAsync();
				if (!action.IsSuccess)
				{
					return BadRequest(new { error = action.Message });
				}
				if (action.Value == null)
				{
					return StatusCode(500, new { error = "Internal server error" });
				}

				return Ok(action.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetAllDocs: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
        }

		[HttpGet("{docId}")]
		public async Task<IActionResult> GetDoc([FromRoute] int docId)
		{
			try
			{
				var action = await _docService.GetAsync(docId);
				if (!action.IsSuccess)
				{
					return BadRequest(new { error = action.Message });
				}

				return Ok(action.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetDoc: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpDelete("{docId}")]
		[Authorize]
		public async Task<IActionResult> DeleteDoc([FromRoute] int docId)
		{
			try
			{
				_docService.Actor = HttpContext.Items["Actor"] as Unit;

				var action = await _docService.DeleteAsync(docId);
				if (!action.IsSuccess)
				{
					return BadRequest(new { error = action.Message });
				}

				return Ok();
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetDoc: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}
	}
}
