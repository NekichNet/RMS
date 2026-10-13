using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.SingleDayEvents;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Business.Services
{
    public class RewardService : BusinessService
    {
		private readonly ILogger<RewardService> _logger;
		private readonly AppDbContext _db;
        private readonly DocService _docService;
        private readonly UnitPermissionService _unitPermissionService;

        public RewardService(
            AppDbContext db,
            DocService docService,
            UnitPermissionService unitPermissionService,
            ILogger<RewardService> logger)
        {
            _db = db;
            _docService = docService;
            _unitPermissionService = unitPermissionService;
			_logger = logger;
		}

        public async Task<ActionResult<Reward>> CreateAsync(
            string name,
            string color,
            string conditions,
            string privileges
        )
        {
            ActionResult<Reward> action = new ActionResult<Reward>(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRewards();
                if (!result.IsSuccess)
                    return action.FormFailure("Creating reward restricted. Permission check failed", eventId: EventIds.Forbidden);

                action.Value = new Reward
                {
                    Name = name,
                    Conditions = conditions,
                    Privileges = privileges,
                    Color = color
                };

                await _db.Rewards.AddAsync(action.Value);

				action.Value.UpdateRole();
                await _db.SaveChangesAsync();
				action.FormSuccess($"Reward {action.Value.Name} created");
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<Reward>> GetAsync(int rewardId)
        {
            ActionResult<Reward> action = new ActionResult<Reward>(_logger);

            try
            {
                action.Value = await _db.Rewards.FindAsync(rewardId);

                if (action.Value != null)
                    action.FormSuccess($"Reward {rewardId} found");
                else
                    action.FormFailure($"Reward with ID {rewardId} not found");
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<List<Reward>>> GetAllAsync()
        {
            ActionResult<List<Reward>> action = new ActionResult<List<Reward>>(_logger);

            try
            {
                action.Value = await _db.Rewards.ToListAsync();

                action.FormSuccess("Reward list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<Reward>>> GetAllActualAsync()
		{
			ActionResult<List<Reward>> action = new ActionResult<List<Reward>>(_logger);

			try
			{
				action.Value = await _db.Rewards.Where(r => r.CanBeAssigned).ToListAsync();

				action.FormSuccess("Reward list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> UpdateAsync(int rewardId, string name, string color, string conditions, string privileges)
        {
            EmptyAction action = new EmptyAction(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRewards();
                if (!result.IsSuccess)
                    return action.FormFailure("Updating reward restricted. Permission check failed", eventId: EventIds.Forbidden);

                Reward? reward = await _db.Rewards.FindAsync(rewardId);

                if (reward == null)
                    return action.FormFailure($"Updating reward failed. Reward with ID {rewardId} not found", eventId: EventIds.NotFound);

                reward.Name = name;
                reward.Conditions = conditions;
                reward.Privileges = privileges;
                reward.Color = color;

                _db.Rewards.Update(reward);
                await _db.SaveChangesAsync();

                reward.UpdateRole();

                action.FormSuccess($"Reward {reward.Name} updated", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<ulong?>> UpdateRoleAsync(int rewardId)
        {
			ActionResult<ulong?> action = new ActionResult<ulong?>(_logger);

            try
            {
                EmptyAction result = await CheckCanManageRewards();
                if (!result.IsSuccess)
                    return action.FormFailure("Updating reward Discord role restricted. Permission check failed", eventId: EventIds.Forbidden);

                Reward? reward = await _db.Rewards.FindAsync(rewardId);

                if (reward == null)
                    return action.FormFailure($"Updating reward Discord role failed. Reward with ID {rewardId} not found", eventId: EventIds.NotFound);

				reward.UpdateRole();
				action.Value = reward.DiscordRoleId;

				await _db.SaveChangesAsync();

				action.FormSuccess($"Reward {reward.Name} Discord role updated", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<List<AssignedReward>>> GetRewardAssignmentsAsync(int rewardId)
        {
            ActionResult<List<AssignedReward>> action = new ActionResult<List<AssignedReward>>(_logger);

            try
            {
                Reward? reward = await _db.Rewards.FindAsync(rewardId);
                if (reward == null)
                    return action.FormFailure($"Getting reward assignments failed. Reward with ID {rewardId} not found", eventId: EventIds.NotFound);

                action.Value = reward.AssignedRewards.ToList();
                action.FormSuccess($"Assigned {reward.Name} rewards retrieved, length: " + action.Value.Count,
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<Unit>>> GetUnitsByRewardAsync(int rewardId)
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				Reward? reward = await _db.Rewards.FindAsync(rewardId);
				if (reward == null)
					return action.FormFailure($"Getting units by reward failed. Reward with ID {rewardId} not found", eventId: EventIds.NotFound);

				action.Value = reward.AssignedRewards.Select(ar => ar.Unit).ToList();
				action.FormSuccess($"Units by {reward.Name} reward with ID {rewardId} retrieved, length: " + action.Value.Count,
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<AssignedReward>>> AssignMultipleAsync(HashSet<ulong> unitIds, HashSet<int> rewardIds, int? docId = null)
        {
            ActionResult<List<AssignedReward>> action = new ActionResult<List<AssignedReward>>(_logger);

            try
            {
                if (Actor == null)
					return action.FormFailure("Assigning rewards restricted. Unauthorized", eventId: EventIds.Unauthorized);

				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignRewards)).IsSuccess)
					return action.FormFailure($"Assigning rewards restricted. {Actor.Nickname} doesn't have AssignRewards permission", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Assigning rewards failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				List<Unit> units = new List<Unit>();

				foreach (ulong unitId in unitIds)
				{
					Unit? unit = await _db.Units.FindAsync(unitId);
					if (unit == null)
					{
						_logger.LogWarning(eventId: EventIds.NotFound, $"Assigning reward failed. Unit with ID {unitId} not found");
						continue;
					}
                    units.Add(unit);
				}

				if (!units.Any())
					return action.FormFailure($"Assigning rewards failed. Not any valid unit Discord ID provided", eventId: EventIds.NotFound);

                List<AssignedReward> rewardAssignments = new List<AssignedReward>();
                List<Reward> rewards = new List<Reward>();

                foreach (int rewardId in rewardIds)
                {
					Reward? reward = await _db.Rewards.FindAsync(rewardId);
					if (reward == null)
                    {
						_logger.LogWarning(eventId: EventIds.NotFound, $"Assigning reward failed. Reward with ID {rewardId} not found");
                        continue;
					}

                    bool wasAssigned = false;
					foreach (Unit unit in units)
					{
						AssignedReward? existingAssignment = await _db.AssignedRewards
							.FirstOrDefaultAsync(ar => ar.RewardId == reward.Id && ar.UnitId == unit.DiscordId);

						if (existingAssignment != null)
						{
							_logger.LogWarning(eventId: EventIds.ImpossibleAction,
								$"Assigning reward failed. Unit {unit.Nickname} already assigned to reward {reward.Name}");
							continue;
						}

                        AssignedReward assignedReward = new AssignedReward { Unit = unit, Reward = reward };
                        rewardAssignments.Add(assignedReward);
                        wasAssigned = true;
					}

                    if (wasAssigned)
                        rewards.Add(reward);
                }

				if (!rewardAssignments.Any())
					return action.FormFailure($"Assigning rewards failed. Not any valid reward ID provided", eventId: EventIds.NotFound);

                RewardAssignmentEvent rewardAssignmentEvent = new RewardAssignmentEvent()
                {
                    AssignedRewards = rewardAssignments,
                    Units = rewardAssignments.Select(ra => ra.Unit).ToHashSet().ToList(),
                    DocId = docId,
                };

				await _db.AssignedRewards.AddRangeAsync(rewardAssignments);
                rewardAssignmentEvent.AssignedRewards = rewardAssignments;
                await _db.RewardAssignmentEvents.AddAsync(rewardAssignmentEvent);
                await _db.SaveChangesAsync();

				action.Value = rewardAssignments;
				action.FormSuccess($"{rewardAssignmentEvent.Units.Count} units were assigned to {rewards.Count} rewards", eventId: EventIds.Updated);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<AssignedReward>> GetAssignedRewardAsync(int rewardId, ulong unitId)
        {
            ActionResult<AssignedReward> action = new ActionResult<AssignedReward>(_logger);

            try
            {
                AssignedReward? assignedReward = await _db.AssignedRewards
                    .FirstOrDefaultAsync(ar => ar.RewardId == rewardId && ar.UnitId == unitId);

                if (assignedReward == null)
                    return action.FormFailure($"Unit with ID {unitId} not assigned to reward {rewardId}", eventId: EventIds.NotFound);

                action.Value = assignedReward;
                action.FormSuccess($"Unit {assignedReward.Unit.Nickname}'s reward {assignedReward.Reward.Name} assignment retrieved", eventId: EventIds.Read);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        public async Task<ActionResult<Reward>> CheckCanAssignReward(int rewardId)
        {
			ActionResult<Reward> action = new ActionResult<Reward>(_logger);

			try
			{
				

				action.FormSuccess($"{Actor.Nickname} can assign {action.Value.Name} reward", eventId: EventIds.Accessed);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> CheckCanManageRewards()
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Permission check failed. Unauthorized", eventId: EventIds.Unauthorized);

				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.ManageRewards)).IsSuccess)
					return action.FormFailure($"Permission check failed. {Actor.Nickname} doesn't have ManageRewards permission", eventId: EventIds.Forbidden);

				action.FormSuccess($"{Actor.Nickname} can manage rewards", eventId: EventIds.Accessed);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
	}
}
