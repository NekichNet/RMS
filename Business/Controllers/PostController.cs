using Business.Models;
using Business.Models.Dto.Acts;
using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Business.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly PostService _postService;
        private readonly ILogger<PostController> _logger;

        public PostController(PostService postService, ILogger<PostController> logger)
        {
            _postService = postService;
            _logger = logger;
        }

		[HttpPost]
		[Authorize]
		public async Task<IActionResult> CreatePost([FromBody] PostDto dto)
		{
			try
			{
				_postService.Actor = HttpContext.Items["Actor"] as Unit;

				var newPost = await _postService.CreateAsync(
					dto.Name,
					dto.Description,
					dto.SubdivisionId,
					dto.HeadId,
					dto.MaxRankId,
					dto.Color,
					dto.AppendSubdivisionName
				);
				if (!newPost.IsSuccess)
				{
					return BadRequest(new { error = newPost.Message });
				}

				return Ok(newPost.Value);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in CreatePost: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}

		[HttpGet]
        public async Task<IActionResult> GetAllPosts()
        {
            try
            {
                var action = await _postService.GetAllAsync();
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return StatusCode(500, new { error = "Internal server error" });
                }

                return Ok(action.Value.Select(p => p.ToDto()).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetAllPosts: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }


        [HttpGet("{postId}")]
        public async Task<IActionResult> GetPost([FromRoute] int postId)
        {
            try
            {
                var action = await _postService.GetAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(action.Value.ToDto());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetPost: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{postId}/permission")]
        public async Task<IActionResult> GetPostPermissions([FromRoute] int postId)
        {
            try
            {
                var action = await _postService.GetAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                var permissions = action.Value.GetPermissionsRecursive();
                return Ok(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetPostPermissions: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

		[HttpPost("{postId}/permission")]
		[Authorize]
		public async Task<IActionResult> SetPostPermissions(
			[FromRoute] int postId,
			[FromBody] List<GivePermissionDto> permissionDtos
			)
		{
			try
			{
				_postService.Actor = HttpContext.Items["Actor"] as Unit;

				var action = await _postService.UpdatePermissionsAsync(postId, permissionDtos);
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
				_logger.LogError($"Error in SetPostPermissions: {ex.Message}");
				return StatusCode(500, new { error = "Internal server error" });
			}
		}


		[HttpGet("{postId}/discord-role")]
        public async Task<IActionResult> GetPostDiscordRole([FromRoute] int postId)
        {
            try
            {
                var action = await _postService.GetAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                if (action.Value.DiscordRoleId == null)
                {
                    return Ok(new { discord_role_id = "" });
                }

                return Ok(new { discord_role_id = action.Value.DiscordRoleId.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetPostDiscordRole: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{postId}")]
        [Authorize]
        public async Task<IActionResult> DeletePost([FromRoute] int postId)
        {
            try
            {
                _postService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _postService.DeleteAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeletePost: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPatch("{postId}")]
        [Authorize]
        public async Task<IActionResult> UpdatePost([FromRoute] int postId, [FromBody] PostDto dto)
        {
            try
            {
                _postService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _postService.UpdateAsync(
                    postId,
                    dto.Name,
                    dto.AppendSubdivisionName,
                    dto.Description,
                    dto.Color,
                    dto.SubdivisionId,
                    dto.MaxRankId,
                    dto.HeadId
                );

                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                return Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdatePost: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("{postId}/discord-role")]
        [Authorize]
        public async Task<IActionResult> UpdatePostRole([FromRoute] int postId)
        {
            try
            {
                _postService.Actor = HttpContext.Items["Actor"] as Unit;
                
                var action = await _postService.UpdateRoleAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                return Ok(action);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in UpdatePostRole: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{postId}/assign")]
        public async Task<IActionResult> GetUnitsByPost([FromRoute] int postId)
        {
            try
            {
                var action = await _postService.GetUnitsByPostAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(action.Value.Select(u => u.ToDto()));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetAssignedUnit: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("assign")]
        [Authorize]
        public async Task<IActionResult> AssignPost([FromBody] PostAssignActDto actDto)
        {
            try
            {
                _postService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _postService.AssignMultipleAsync(actDto.UnitIds.Select(i => ulong.Parse(i)).ToHashSet(), actDto.PostIds, actDto.Overwrite, actDto.DocId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                return Ok(action.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in AssignPost: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("{postId}/assign/{unitId}")]
        public async Task<IActionResult> GetInfoAssignedUnit([FromRoute] int postId, [FromRoute] ulong unitId)
        {
            try
            {
                var action = await _postService.GetAsync(postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }
                if (action.Value == null)
                {
                    return NotFound(new { error = "PostId not found" });
                }

                var assignedPost = action.Value.AssignedPosts.FirstOrDefault(ap => ap.Unit.DiscordId == unitId && ap.IsActive());
                if (assignedPost == null)
                {
                    return Ok(null);
                }

                return Ok(assignedPost);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetInfoAssignedUnit: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{postId}/assign/{unitId}")]
        [Authorize]
        public async Task<IActionResult> DeposeUnit([FromRoute] int postId, [FromRoute] ulong unitId)
        {
            try
            {
                _postService.Actor = HttpContext.Items["Actor"] as Unit;

                var action = await _postService.DeposeAsync(unitId, postId);
                if (!action.IsSuccess)
                {
                    return BadRequest(new { error = action.Message });
                }

                return Ok(new { message = action.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in DeposeUnit: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
