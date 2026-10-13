using Business.Database;
using Business.Logging;
using Business.Models.Enums;
using Business.Models.Util;
using Business.Services;
using Business.Services.Abstraction;

namespace Business.Models
{
	public class UnitPermissionService : BusinessService
	{
		private readonly AppDbContext _db;
		private readonly RankPermissionService _rankPermissionService;

		public UnitPermissionService(AppDbContext db, RankPermissionService rankPermissionService, ILogger logger) : base(logger)
		{
			_db = db;
			_rankPermissionService = rankPermissionService;
		}

		public async Task<EmptyAction> CheckUnitHasPermissionAsync(ulong unitId, PermissionType permissionType)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				ActionResult<HashSet<Permission>> result = await GetUnitPermissionsAsync(unitId);
				if (!result.IsSuccess)
					return action.FormFailure($"Checking permission {permissionType.ToString()} in unit with ID {unitId} failed", eventId: EventIds.Failed);

				if (result.Value.Any(p => p.Type == permissionType))
					action.FormSuccess($"Unit with ID {unitId} has {permissionType.ToString()} permission", eventId: EventIds.Ok);
				else
					action.FormFailure($"Unit with ID {unitId} does not have {permissionType.ToString()} permission", eventId: EventIds.Ok);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Permission>>> CheckUnitIsAdminAsync(ulong unitId)
		{
			ActionResult<HashSet<Permission>> action = new ActionResult<HashSet<Permission>>(_logger);

			try
			{
				ActionResult<HashSet<Permission>> result = await GetUnitPermissionsAsync(unitId);
				if (!result.IsSuccess)
					return action.FormFailure($"Checking unit being an admin with ID {unitId} failed", eventId: EventIds.Failed);

				if (result.Value.Any(p => p.Type == PermissionType.Administrator))
					action.FormSuccess($"Unit with ID {unitId} is an admin", eventId: EventIds.Ok);
				else
					action.FormFailure($"Unit with ID {unitId} is not an admin", eventId: EventIds.Ok);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Permission>>> GetUnitPermissionsAsync(ulong unitId)
		{
			ActionResult<HashSet<Permission>> action = new ActionResult<HashSet<Permission>>(_logger);

			try
			{
				Unit? unit = await _db.Units.FindAsync(unitId);

				if (unit != null)
				{
					action.Value = new HashSet<Permission>();

					Rank? rank = unit.GetRank();
					if (rank != null)
					{
						ActionResult<HashSet<Permission>> result = await _rankPermissionService.GetRankPermissionsAsync(rank.Id, rank);
						if (result.IsSuccess)
							action.Value.UnionWith(result.Value);
					}

					action.Value.UnionWith(unit.GetPosts().SelectMany(p => p.GetPermissionsRecursive()));

					action.FormSuccess("Unit permissions list formed, length: " + action.Value.Count(),
						eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
				}
				else
				{
					action.FormFailure($"Getting unit permissions failed. Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
	}
}
