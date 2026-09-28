using Business.Models;
using Business.Models.Dto.Acts;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Business.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class RankController : ControllerBase
    {
        private readonly RankService _rankService;
        private readonly ILogger<RankController> _logger;

        public RankController(RankService rankService, ILogger<RankController> logger)
        {
            _rankService = rankService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRanks()
        {
            try
            {
                var action = await _rankService.GetAllAsync();
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
                _logger.LogError($"Error in GetAllRanks: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{rankId}")]
        public async Task<IActionResult> GetRank([FromRoute] int rankId)
        {
            try
            {
                var action = await _rankService.GetAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "Rank not found" });
                }

                return Ok(action.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{rankId}/permission")]
        public async Task<IActionResult> GetRankPermissions([FromRoute] int rankId)
        {
            try
            {
                var action = await _rankService.GetRankPermissionsAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "Rank not found" });
                }

                var permissions = action.Value;
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetRankPermissions: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

		[HttpPost("{rankId}/permission")]
        [Authorize]
		public async Task<IActionResult> SetRankPermissions(
            [FromRoute] int rankId,
            [FromBody] List<GivePermissionDto> permissionDtos
            )
        {
            try
            {
				_rankService.Actor = HttpContext.Items["Actor"] as Unit;

				var action = await _rankService.UpdatePermissionsAsync(rankId, permissionDtos);
				if (action.IsSuccess)
				{
					return Ok();
				}
                else
				{
					return BadRequest(new { error = action.Message });
				}
			}
            catch (Exception ex)
            {
				_logger.LogError($"Error in SetRankPermissions: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
        }

        [HttpGet("{rankId}/discord-role")]
        public async Task<IActionResult> GetRankDiscordRole([FromRoute] int rankId)
        {
            try
            {
                var action = await _rankService.GetAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "Rank not found" });
                }

                if (action.Value.DiscordRoleId == null)
                {
                    return Ok(new { discord_role_id = "" });
                }

                return Ok(new { discord_role_id = action.Value.DiscordRoleId.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetRankDiscordRole: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateRank([FromBody] Rank dto)
        {
            try
            {
                _rankService.Actor = HttpContext.Items["Actor"] as Unit;

                var newRank = await _rankService.CreateAsync(dto.Name, dto.CounterToReach, dto.Color, dto.LowerId);
                if (!newRank.IsSuccess)
                {
                    return BadRequest(new { error = newRank.Message });
                }
                return Ok(newRank.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in CreateRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
            
        }

        [HttpDelete("{rankId}")]
        [Authorize]
        public async Task<IActionResult> DeleteRank([FromRoute] int rankId)
        {
            try
            {
                _rankService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _rankService.DeleteAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeleteRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPatch("{rankId}")]
        [Authorize]
        public async Task<IActionResult> UpdateRank(
            [FromRoute] int rankId,
            [FromBody] Rank rank)
        {
            try
            {
                _rankService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _rankService.UpdateAsync(rankId, rank.Name, rank.CounterToReach, rank.Color, rank.LowerId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("{rankId}/discord-role")]
        [Authorize]
        public async Task<IActionResult> UpdateRankRole([FromRoute] int rankId)
        {
            try
            {
                _rankService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _rankService.UpdateRoleAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdateRankRole: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{rankId}/assign")]
        public async Task<IActionResult> GetUnitsByRank([FromRoute] int rankId)
        {
            try
            {
                var action = await _rankService.GetUnitsByRankAsync(rankId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(action.Value.Select(u => u.ToDto()));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetUnitsByRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("assign")]
        [Authorize]
        public async Task<IActionResult> AssignRank([FromBody] RankAssignActDto actDto)
        {
            try
            {
                _rankService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _rankService.AssignMultipleAsync(actDto.UnitIds, actDto.RankId, actDto.DocId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AssignRank: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

		[HttpPost("change")]
		[Authorize]
		public async Task<IActionResult> ChangeRank([FromBody] RankChangeActDto actDto)
		{
			try
			{
				_rankService.Actor = HttpContext.Items["Actor"] as Unit;

				var action = await _rankService.ChangeMultipleAsync(
                    actDto.UnitIds,
                    actDto.Steps,
                    actDto.IgnorePostMaxRank,
                    actDto.IsDowngrade,
                    actDto.DocId
                    );

				if (!action.IsSuccess)
				{
					return BadRequest(new { error = action.Message });
				}
				return Ok(new { message = action.Message });
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in ChangeRank: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet("{rankId}/assign/{unitId}")]
        public async Task<IActionResult> GetAssignedUnit([FromRoute] int rankId, [FromRoute] ulong unitId)
        {
            try
            {
                var action = await _rankService.GetAssignedRankAsync(rankId, unitId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return Ok(null);
                }
                return Ok(action.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetAssignedUnit: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}