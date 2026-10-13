using Business.Logging;
using Business.Models;
using Business.Models.Dto;
using Business.Models.Dto.Acts;
using Business.Models.Dto.Acts.Abstraction;
using Business.Models.States.Statuses;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Business.Controllers
{
	[Route("api/v1/[controller]")]
	[ApiController]
	public class UnitController : ControllerBase
	{
		private readonly UnitService _unitService;
		private readonly RankService _rankService;
		private readonly PostService _postService;
		private readonly UnitPermissionService _unitPermissionService;
		private readonly ILogger<UnitController> _logger;

		public UnitController(
			UnitService unitService,
			RankService rankService,
			PostService postService,
			UnitPermissionService unitPermissionService,
			ILogger<UnitController> logger)
		{
			_unitService = unitService;
			_rankService = rankService;
			_postService = postService;
			_unitPermissionService = unitPermissionService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetUnits()
		{
			try
			{
				var result = await _unitService.GetAllUnitsAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				IEnumerable<UnitDto> unitsDto = result.Value.Select(u => u.ToDto());

				return Ok(unitsDto);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPost]
		[Authorize]
		public async Task<IActionResult> RegisterUnit([FromBody] NewUnitDto newUnit)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.RegisterAsync(newUnit);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in RegisterUnit: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{unitId}")]
		public async Task<IActionResult> GetUnit([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitService.GetAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				if (result.Value == null)
				{
					_logger.LogWarning($"Unit not found: Discord ID {unitId}");
					return NotFound(new { error = "Unit not found" });
				}

				UnitDto unitResult = result.Value.ToDto();

				return Ok(unitResult);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnit: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{unitId}/status")]
		public async Task<IActionResult> GetUnitStatuses([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitService.GetUnitStatusesAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnitStatuses: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPost("status")]
		[Authorize]
		public async Task<IActionResult> AppendMultipleStatus([FromBody] StatusAssignActDto actDto)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				if (actDto.StatusKey > typeof(StatusType).GetEnumValues().Length)
				{
					return BadRequest(new { error = "Appending status failed. Invalid status key" });
				}

				var result = await _unitService.ApplyStatusMultipleAsync(
					(StatusType)actDto.StatusKey,
					actDto.UnitIds,
					actDto.Ovewrite,
					actDto.End,
					actDto.Days,
					actDto.DocId
					);

				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in AppendMultipleStatus: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{unitId}/activity")]
		public async Task<IActionResult> GetUnitActivity([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitService.GetUnitActivityAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				if (result.Value == null)
				{
					_logger.LogWarning($"Unit not found: Discord ID {unitId}");
					return NotFound(new { error = "Unit not found" });
				}
				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnitActivity: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{unitId}/permissions")]
		public async Task<IActionResult> GetUnitPermissions([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitPermissionService.GetUnitPermissionsAsync(unitId);

				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnitPermissions: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPut("{unitId}/activity")]
		[Authorize]
		public async Task<IActionResult> FixActivity([FromRoute] ulong unitId, [FromBody] DateOnly date)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.FixActivityAsync(unitId, date);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in FixActivity: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPut("activity")]
		[Authorize]
		public async Task<IActionResult> FixMultipleActivity([FromBody] MultipleActivityDto dto)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.FixMultipleActivityAsync(dto.UnitIds, dto.Date);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in FixMultipleActivity: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("actual")]
		public async Task<IActionResult> GetActiveUnits()
		{
			try
			{
				var result = await _unitService.GetActiveUnitsAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				IEnumerable<UnitDto> unitsDto = result.Value.Select(u => u.ToDto());

				return Ok(unitsDto);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetActiveUnits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPost("actual")]
		[Authorize]
		public async Task<IActionResult> ReturnToActiveMultipleUnits([FromBody] ReturnToActiveActDto actDto)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.ReturnToActiveMultipleAsync(
					actDto.UnitIds,
					actDto.PostIds,
					actDto.RankId,
					actDto.DocId
					);

				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in DismissMultipleUnit: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("dismissed")]
		public async Task<IActionResult> GetDismissedUnits()
		{
			try
			{
				var result = await _unitService.GetDismissedUnitsAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				IEnumerable<UnitDto> unitsDto = result.Value.Select(u => u.ToDto());

				return Ok(unitsDto);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetDismissedUnits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("retirement")]
		public async Task<IActionResult> GetRetiredUnits()
		{
			try
			{
				var result = await _unitService.GetRetiredUnitsAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				IEnumerable<UnitDto> unitsDto = result.Value.Select(u => u.ToDto());

				return Ok(unitsDto);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetRetiredUnits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpPost("retirement")]
		[Authorize]
		public async Task<IActionResult> AssignRetirementMultipleUnits([FromBody] ActDto actDto)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.AssignRetirenmentMultipleAsync(actDto.UnitIds, actDto.DocId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in AssignRetirementMultipleUnits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpDelete]
		[Authorize]
		public async Task<IActionResult> DismissMultipleUnits([FromBody] ActDto actDto)
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.DismissMultipleAsync(actDto.UnitIds, actDto.DocId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}
				return Ok(new { message = result.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in DismissMultipleUnit: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{unitId}/states")]
		public async Task<IActionResult> GetUnitStates([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitService.GetUnitStatesAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetUnitState: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}


		[HttpGet("{unitId}/events")]
		public async Task<IActionResult> GetSingleDayEvents([FromRoute] ulong unitId)
		{
			try
			{
				var result = await _unitService.GetUnitEventsAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetSingleDayEvent: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("backgrounds")]
		public async Task<IActionResult> GetAvailableBackgrounds()
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.GetAvailableBackgrounds();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetAvailableBackgrounds: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("kits")]
		public async Task<IActionResult> GetAvailableKits()
		{
			try
			{
				_unitService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _unitService.GetAvailableKits();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetAvailableKits: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("can/rank")]
		public async Task<IActionResult> GetRankChangeList()
		{
			try
			{
				_rankService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _rankService.GetCanChangeRankUnitsAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetRankChangeList: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("can/rank/{unitId}")]
		public async Task<IActionResult> CheckCanChangeRank(ulong unitId)
		{
			try
			{
				_rankService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _rankService.CheckCanChangeRankAsync(unitId);
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in CheckCanChangeRank: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("can/posts")]
		public async Task<IActionResult> GetPostsCanAssign()
		{
			try
			{
				_postService.Actor = HttpContext.Items["Actor"] as Unit;

				var result = await _postService.GetPostsCanAssignAsync();
				if (!result.IsSuccess)
				{
					return BadRequest(new { error = result.Message });
				}

				return Ok(result.Value.Select(p => p.ToDto()));
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in GetPostsCanAssign: {ex.Message}", ex, EventIds.HandledError);
				return StatusCode(500, new { error = "Internal server error" });
			}
		}
	}
}