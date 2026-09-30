using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.Statuses;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;
using System.Buffers;

namespace Business.Services
{
    public class RankService : BusinessService
    {
        private readonly AppDbContext _db;

        public RankService(
            AppDbContext db,
			ILogger logger)
            : base(logger)
        {
            _db = db;
        }

        public async Task<ActionResult<Rank>> CreateAsync(
            string name,
            ushort counterToReach,
            string color,
            int? lowerId,
            int? index
        )
        {
            ActionResult<Rank> action = new ActionResult<Rank>(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRanksAsync();
                if (!result.IsSuccess)
                    return action.FormFailure("Creating rank restricted. Permission check failed", eventId: EventIds.Forbidden);

                if (index == null)
                {
                    if (lowerId == null)
                        return action.FormFailure("Creating rank failed. Index or lower id was not provided", eventId: EventIds.InvalidInput);
                    Rank? lowerRank = await _db.Ranks.FindAsync(lowerId);
                    if (lowerRank == null)
                        return action.FormFailure("Creating rank failed. Lower rank was not found", eventId: EventIds.NotFound);
					EmptyAction reservingResult = await ReserveIndexAsync(lowerRank.Index - 1);
					if (reservingResult == null)
						return action.FormFailure($"Creating rank failed. Unexpected error while reserving index", eventId: EventIds.HandledError);
					index = lowerRank.Index - 1;
                }

                action.Value = new Rank
                {
                    Name = name,
                    CounterToReach = counterToReach,
                    Color = color,
                    Index = (int)index
                };

                await _db.Ranks.AddAsync(action.Value);
                await _db.SaveChangesAsync();

                action.FormSuccess($"Rank {name} with ID {action.Value.Id} created", eventId: EventIds.Created);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<Rank>> GetAsync(int rankId)
        {
            ActionResult<Rank> action = new ActionResult<Rank>(_logger);

            try
            {
                action.Value = await _db.Ranks.FindAsync(rankId);
                if (action.Value != null)
                    action.FormSuccess($"Rank {action.Value.Name} with ID {rankId} found", eventId: EventIds.Read);
                else
                    action.FormFailure($"Rank with ID {rankId} not found", eventId: EventIds.NotFound);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<List<Rank>>> GetAllAsync()
        {
            ActionResult<List<Rank>> action = new ActionResult<List<Rank>>(_logger);

            try
            {
                action.Value = await _db.Ranks.ToListAsync();
                action.FormSuccess("Rank list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<Rank>>> GetAllRanksHigherAsync(int rankId)
		{
			ActionResult<List<Rank>> action = new ActionResult<List<Rank>>(_logger);

			try
			{
				Rank? rank = await _db.Ranks.FindAsync(rankId);
				if (rank == null)
					return action.FormFailure($"Getting all ranks higher failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				action.Value = await _db.Ranks.Where(r => r.Index > rank.Index).ToListAsync();
				action.FormSuccess("Higher ranks list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Rank>>> GetAllRanksLowerAsync(int rankId)
		{
			ActionResult<List<Rank>> action = new ActionResult<List<Rank>>(_logger);

			try
			{
				Rank? rank = await _db.Ranks.FindAsync(rankId);
				if (rank == null)
					return action.FormFailure($"Getting all ranks lower failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				action.Value = await _db.Ranks.Where(r => r.Index < rank.Index).ToListAsync();
				action.FormSuccess("Lower ranks list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> DeleteAsync(int rankId)
        {
            EmptyAction action = new EmptyAction(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRanksAsync();
                if (!result.IsSuccess)
                    return action.FormFailure("Rank deleting restricted. Permission check failed", eventId: EventIds.Forbidden);

                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure("Rank deleting failed. Rank not found", eventId: EventIds.NotFound);

                _db.Ranks.Remove(rank);

                await _db.SaveChangesAsync();

                action.FormSuccess("Rank deleted");
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<EmptyAction> UpdateAsync(
            int rankId,
            string name,
            ushort counterToReach,
            string color,
            int? lowerId,
            int? index
        )
        {
            EmptyAction action = new EmptyAction(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRanksAsync();
                if (!result.IsSuccess)
                    return action.FormFailure("Rank updating restricted. Permission check failed", eventId: EventIds.Forbidden);

                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure("Rank updating failed. Rank not found", eventId: EventIds.NotFound);

				if (index == null)
				{
					if (lowerId == null)
						return action.FormFailure("Creating rank failed. Index or lower id was not provided", eventId: EventIds.InvalidInput);
					Rank? lowerRank = await _db.Ranks.FindAsync(lowerId);
					if (lowerRank == null)
						return action.FormFailure("Creating rank failed. Lower rank was not found", eventId: EventIds.NotFound);
					EmptyAction reservingResult = await ReserveIndexAsync(lowerRank.Index - 1);
					if (reservingResult == null)
						return action.FormFailure($"Creating rank failed. Unexpected error while reserving index", eventId: EventIds.HandledError);
					index = lowerRank.Index - 1;
				}

                rank.Name = name;
				rank.CounterToReach = counterToReach;
                rank.Color = color;
                rank.Index = (int)index;

                rank.UpdateRole();

				_db.Ranks.Update(rank);
                await _db.SaveChangesAsync();

                action.FormSuccess($"Rank {name} updated", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<EmptyAction> UpdateIndexAsync(int rankId, int newIndex)
        {
            EmptyAction action = new EmptyAction(_logger);

            try
            {
                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure($"Updating rank index failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);
                EmptyAction result = await ReserveIndexAsync(newIndex);
                if (result == null)
					return action.FormFailure($"Updating rank index failed. Unexpected error while reserving index", eventId: EventIds.HandledError);
				rank.Index = newIndex;

				await _db.SaveChangesAsync();
				action.FormSuccess($"Set {newIndex} index for rank {rank.Name} with ID {rankId}", eventId: EventIds.Updated);
			}
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<EmptyAction> ReserveIndexAsync(int index)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
                List<Rank> blockingRanks = await _db.Ranks.Where(r => r.Index == index).OrderBy(r => r.Index).ToListAsync();
                for (int i = 0; i < blockingRanks.Count; i++)
                {
					await ReserveIndexAsync(blockingRanks[i].Index - blockingRanks.Count);
                    blockingRanks[i].Index -= blockingRanks.Count;
				}

                await _db.SaveChangesAsync();
                action.FormSuccess($"Reserved rank index {index}. {blockingRanks.Count} rank indexes moved", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<ulong?>> UpdateRoleAsync(int rankId)
        {
			ActionResult<ulong?> action = new ActionResult<ulong?>(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRanksAsync();
                if (!result.IsSuccess)
                    return action.FormFailure("Updating rank role restricted. Permission check failed", eventId: EventIds.Forbidden);

                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure($"Updating rank role failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				rank.UpdateRole();
                action.Value = rank.DiscordRoleId;

				_db.Ranks.Update(rank);
                await _db.SaveChangesAsync();

                action.FormSuccess($"Rank {rank.Name} with ID {rankId} Discord role updated", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		/// <summary>
		/// Устанавливает званию разрешения по переданным permission ID.
		/// Перезаписывает только разрешения, выданные конкретно этому званию,
		/// а не унаследованные разрешения от более низких званий.
		/// Попытка снять или установить разрешение, которого нет у пользователя
		/// будет проигнорированна.
		/// </summary>
		public async Task<EmptyAction> UpdatePermissionsAsync(int rankId, List<GivePermissionDto> permissionDtos)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				EmptyAction result = await CheckCanManageRanksAsync();
				if (!result.IsSuccess)
					return action.FormFailure("Updating rank permissions restricted. Permission check failed", eventId: EventIds.Forbidden);

                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure($"Updating rank permissions failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				List<GivedPermission<Rank>> givedPermissions = rank.GivedPermissions.ToList();
				int permissionsHad = givedPermissions.Count;

				foreach (GivedPermission<Rank> givedPermission in givedPermissions)
				{
					if (Actor.HasPermission(givedPermission.PermissionType))
						_db.RankPermissions.Remove(givedPermission);
				}

				foreach (GivePermissionDto permissionDto in permissionDtos)
				{
					if (permissionDto.PermissionId > 0 && permissionDto.PermissionId <= typeof(PermissionType).GetEnumValues().Length)
					{
						PermissionType permissionType = (PermissionType)permissionDto.PermissionId;
						if (Actor.HasPermission(permissionType) && !(await CheckHasPermissionAsync(rank.Id, permissionType)).IsSuccess)
						{
							Permission? permission = await _db.Permissions.FindAsync(permissionType);
							if (permission != null)
							{
                                _db.RankPermissions.Add(new GivedPermission<Rank>
								{
									Permission = permission,
									Inherit = permissionDto.Inherit,
									EntityId = rankId
								});
							}
						}
					}
				}

                await _db.SaveChangesAsync();

				action.FormSuccess($"Rank {rank.Name} with ID {rankId} permissions updated." +
					$"Then {permissionsHad}, now {rank.GivedPermissions.Count}", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

        public async Task<EmptyAction> CheckHasPermissionAsync(int rankId, PermissionType permissionType)
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
                ActionResult<List<Rank>> result = await GetAllRanksLowerAsync(rankId);
                if (!result.IsSuccess)
                    return action.FormFailure($"Getting rank with ID {rankId} gived permissions failed", eventId: EventIds.Failed);

                action.Value = result.Value.SelectMany(r => r.GivedPermissions.Where(gp => gp.Inherit)).ToHashSet();
				action.FormSuccess("Rank gived permissions list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
            catch (Exception ex)
            {
                return action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<HashSet<Permission>>> GetRankPermissionsAsync(int rankId)
		{
			ActionResult<HashSet<Permission>> action = new ActionResult<HashSet<Permission>>(_logger);

			try
			{
				ActionResult<List<Rank>> result = await GetAllRanksLowerAsync(rankId);
				if (!result.IsSuccess)
					return action.FormFailure($"Getting rank permissions failed", eventId: EventIds.Failed);

				action.Value = result.Value.SelectMany(r => r.GivedPermissions.Where(gp => gp.Inherit).Select(gp => gp.Permission)).ToHashSet();
				action.FormSuccess("Rank permissions list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Unit>>> GetUnitsByRankAsync(int rankId)
        {
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

            try
            {
                Rank? rank = await _db.Ranks.FindAsync(rankId);
                if (rank == null)
                    return action.FormFailure($"Getting units by rank failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

                action.Value = rank.AssignedRanks.Where(r => r.IsActive()).Select(ar => ar.Unit).ToList();

                action.FormSuccess($"Units by {rank.Name} rank with ID {rankId} retrieved",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<List<AssignedRank>>> AssignMultipleAsync(HashSet<ulong> unitIds, int rankId, int? docId = null)
        {
			ActionResult<List<AssignedRank>> action = new ActionResult<List<AssignedRank>>(_logger);

            try
            {
				if (Actor == null)
					return action.FormFailure("Permission check failed. Unauthorized", eventId: EventIds.Unauthorized);
				if (!Actor.HasPermission(PermissionType.AssignRanks))
					return action.FormFailure($"{Actor.Nickname} don't have AssignRanks permission", eventId: EventIds.Forbidden);

				Rank? rank = await _db.Ranks.FindAsync(rankId);
				if (rank == null)
					return action.FormFailure($"Rank assigning failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				List<AssignedRank> assignedRanks = new List<AssignedRank>();

                foreach (ulong unitId in unitIds)
                {
					ActionResult<Unit> result = await CheckCanChangeRankAsync(unitId);
					if (!result.IsSuccess)
                    {
						_logger.LogWarning(eventId: EventIds.Forbidden,
                            $"Rank assigning to unit with Discord ID {unitId} restricted. Permission check failed");
                        continue;
					}

					AssignedRank? currentAssignment = result.Value.GetAssignedRank();

                    if (currentAssignment == null)
                    {
						_logger.LogError(eventId: EventIds.HandledError,
                            $"Rank assigning to unit with Discord ID {unitId} failed. Can't achieve current assignment");
						continue;
					}

					if (currentAssignment.Rank.Id == rank.Id)
                    {
						_logger.LogInformation(eventId: EventIds.Forbidden, $"Rank assigning failed. Unit {result.Value.Nickname}" +
							$" already assigned to rank {rank.Name} with ID {rankId}");
						continue;
					}

					currentAssignment.Terminate();

					var newAssignedRank = new AssignedRank
					{
						UnitId = result.Value.DiscordId,
						RankId = rankId,
						Start = DateTime.UtcNow
					};

					await _db.AssignedRanks.AddAsync(newAssignedRank);
                    assignedRanks.Add(newAssignedRank);
				}

				action.Value = assignedRanks;

				await _db.SaveChangesAsync();

                action.FormSuccess($"{assignedRanks.Count} units were assigned to rank {rank.Name}", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<AssignedRank>>> ChangeMultipleAsync(
            HashSet<ulong> unitIds,
            int steps = 1,
            bool ignorePostMaxRank = false,
            bool isDowngrade = false,
            int? docId = null
            )
		{
			ActionResult<List<AssignedRank>> action = new ActionResult<List<AssignedRank>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Changing rank restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!Actor.HasPermission(PermissionType.AssignRanks))
					return action.FormFailure($"Changing rank restricted." +
                        $" {Actor.Nickname} don't have AssignRanks permission", eventId: EventIds.Forbidden);

                if (isDowngrade)
                    steps = -steps;

                if (steps == 0)
                    return action.FormFailure($"Changing rank failed. Cannot set zero steps", eventId: EventIds.ImpossibleAction);

				List<AssignedRank> assignedRanks = new List<AssignedRank>();

				foreach (ulong unitId in unitIds)
				{
					ActionResult<Unit> result = await CheckCanChangeRankAsync(unitId);
					if (!result.IsSuccess)
					{
						_logger.LogWarning(eventId: EventIds.Forbidden,
                            $"Unit with Discord Id {unitId} Rank changing restricted. Permission check failed");
						continue;
					}

                    AssignedRank? currentAssignment = result.Value.GetAssignedRank();
                    
					if (currentAssignment == null)
					{
						_logger.LogError(eventId: EventIds.HandledError,
							$"Unit {result.Value.Nickname} rank changing failed. Can't get unit's current rank");
						continue;
					}

					Rank currentRank = currentAssignment.Rank;
					Rank? maxRank = result.Value.GetMaxRank();

					if (maxRank == null)
					{
						_logger.LogError(eventId: EventIds.HandledError,
							$"Unit {result.Value.Nickname} rank changing failed. Can't get unit's max rank");
						continue;
					}

                    Rank targetRank;
                    if (steps > 0)
                    {
                        var higherRanks = await _db.Ranks.Where(r => r.Index >= currentRank.Index).OrderBy(r => r.Index).ToListAsync();
                        steps = higherRanks.Count < steps ? higherRanks.Count : steps;
                        targetRank = higherRanks[steps];
                        if (!ignorePostMaxRank && targetRank.Index > maxRank.Index)
                            targetRank = maxRank;
                    }
                    else
                    {
						var lowerRanks = await _db.Ranks.Where(r => r.Index <= currentRank.Index).OrderByDescending(r => r.Index).ToListAsync();
						steps = lowerRanks.Count < steps ? lowerRanks.Count : steps;
						targetRank = lowerRanks[steps];
					}

					if (targetRank == currentRank)
                    {
						_logger.LogWarning(eventId: EventIds.ImpossibleAction,
							$"Unit {result.Value.Nickname} rank changing failed. Already achieved rank bounds");
						continue;
					}

					currentAssignment.Terminate();

					var newAssignedRank = new AssignedRank
					{
						Unit = result.Value,
						Rank = targetRank,
						Start = DateTime.UtcNow
					};

					await _db.AssignedRanks.AddAsync(newAssignedRank);
					assignedRanks.Add(newAssignedRank);
				}

                action.Value = assignedRanks;

				await _db.SaveChangesAsync();

				action.FormSuccess($"{assignedRanks.Count} units' ranks were " + 
                    (steps > 0 ? "upgraded" : "downgraded") + $" by {steps} steps",
                    eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<AssignedRank>> GetAssignedRankAsync(int rankId, ulong unitDiscordId)
        {
            ActionResult<AssignedRank> action = new ActionResult<AssignedRank>(_logger);

            try
            {
                AssignedRank? assignedRank = _db.AssignedRanks
                    .AsEnumerable()
                    .FirstOrDefault(
                        ar => ar.UnitId == unitDiscordId
                        && ar.RankId == rankId
                        && ar.IsActive(null));

                if (assignedRank == null)
                    return action.FormFailure($"Unit with Discord ID {unitDiscordId} not assigned to rank {rankId}", eventId: EventIds.NotFound);

				action.Value = assignedRank;
                action.FormSuccess($"Unit's with Discord ID {unitDiscordId} assignment to {assignedRank.Rank.Name} retrieved", eventId: EventIds.Read);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<EmptyAction> CheckCanManageRanksAsync()
        {
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't check permissions. Unauthorized", eventId: EventIds.Unauthorized);
				if (!Actor.HasPermission(PermissionType.ManageRanks))
					return action.FormFailure($"{Actor.Nickname} don't have ManageRanks permission", eventId: EventIds.Forbidden);

				action.FormSuccess($"{Actor.Nickname} can manage ranks");
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

        public async Task<ActionResult<Unit>> CheckCanChangeRankAsync(ulong unitDiscordId, Unit? unit = null)
        {
            ActionResult<Unit> action = new ActionResult<Unit>(_logger);

            try
            {
                if (Actor == null)
                    return action.FormFailure("Permission check failed. Unauthorized", eventId: EventIds.Unauthorized);
                if (!Actor.HasPermission(PermissionType.AssignRanks))
                    return action.FormFailure($"{Actor.Nickname} don't have AssignRanks permission", eventId: EventIds.Forbidden);

                if (unit == null)
                    unit = await _db.Units.FindAsync(unitDiscordId);
                if (unit == null)
                    return action.FormFailure("Permission check failed. Unit not found", eventId: EventIds.NotFound);
                action.Value = unit;

                if (!unit.IsActive() && !Actor.IsAdmin())
                    return action.FormFailure($"Permission check failed." +
                        $" Unit {unit.Nickname} is in retirement or dismissed", eventId: EventIds.Forbidden);

				HashSet<Post> headPosts = Actor
									.GetPosts()
									.SelectMany(p => p.GetAllHeadsRecursive())
									.ToHashSet();

				if (!Actor.IsAdmin() && _db.Posts.Except(headPosts).Intersect(unit.GetPosts()).Any())
                    return action.FormFailure("Permission check failed. Can't change heads ranks", eventId: EventIds.Forbidden);

                action.FormSuccess($"{Actor.Nickname} can change {action.Value.Nickname}'s rank");
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<Unit>>> GetCanChangeRankUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Getting available units to change rank failed. Unauthorized", eventId: EventIds.Unauthorized);
				if (!Actor.HasPermission(PermissionType.AssignRanks))
					return action.FormFailure($"{Actor.Nickname} don't have AssignRanks permission", eventId: EventIds.Forbidden);

                if (Actor.IsAdmin())
                {
                    action.Value = await _db.Units.ToListAsync();
                }
                else
                {
					HashSet<Unit> actorHeads = Actor
                        .GetPosts()
					    .SelectMany(p => p.GetAllHeadsRecursive())
					    .SelectMany(p => p.AssignedPosts.Select(ap => ap.Unit))
					    .ToHashSet();

					action.Value = (await _db.Units.Except(actorHeads).ToListAsync()).Where(u => u.IsActive()).ToList();
				}

				action.FormSuccess($"{Actor.Nickname}'s available units to change ranks list formed. Length: {action.Value.Count()}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
	}
}