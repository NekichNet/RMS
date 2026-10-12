using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.Util;
using Business.Services.Abstraction;

namespace Business.Services
{
	public class RankPermissionService : BusinessService
	{
		private readonly AppDbContext _db;

		public RankPermissionService(AppDbContext db, ILogger logger) : base(logger)
		{
			_db = db;
		}

		public async Task<EmptyAction> CheckRankHasPermissionAsync(int rankId, PermissionType permissionType)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				ActionResult<HashSet<Permission>> result = await GetRankPermissionsAsync(rankId);
				if (!result.IsSuccess)
					return action.FormFailure($"Checking permission in rank with ID {rankId} failed", eventId: EventIds.Failed);

				if (result.Value.Any(p => p.Type == permissionType))
					action.FormSuccess($"Rank with ID {rankId} has {permissionType.ToString()} permission", eventId: EventIds.Ok);
				else
					action.FormFailure($"Rank with ID {rankId} does not have {permissionType.ToString()} permission", eventId: EventIds.Ok);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<GivedPermission<Rank>>>> GetRankGivedPermissionsAsync(int rankId)
		{
			ActionResult<HashSet<GivedPermission<Rank>>> action = new ActionResult<HashSet<GivedPermission<Rank>>>(_logger);

			try
			{
				Rank? rank = await _db.Ranks.FindAsync(rankId);
				if (rank == null)
					return action.FormFailure($"Getting post gived permissions failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				action.Value = _db.Ranks.Where(r => r.Index < rank.Index).SelectMany(r => r.GivedPermissions.Where(gp => gp.Inherit)).ToHashSet();
				action.FormSuccess("Rank gived permissions list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Permission>>> GetRankPermissionsAsync(int rankId, Rank? rank = null)
		{
			ActionResult<HashSet<Permission>> action = new ActionResult<HashSet<Permission>>(_logger);

			try
			{
				if (rank == null)
				{
					rank = await _db.Ranks.FindAsync(rankId);
					if (rank == null)
						return action.FormFailure($"Getting post permissions failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);
				}

				action.Value = _db.Ranks.Where(r => r.Index < rank.Index).SelectMany(r => r.GivedPermissions.Where(gp => gp.Inherit).Select(gp => gp.Permission)).ToHashSet();
				action.FormSuccess("Rank permissions list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}
	}
}
