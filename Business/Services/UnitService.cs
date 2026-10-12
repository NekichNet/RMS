using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.SingleDayEvents;
using Business.Models.SingleDayEvents.Abstraction;
using Business.Models.States;
using Business.Models.States.Abstraction;
using Business.Models.States.Statuses;
using Business.Models.Statuses;
using Business.Models.Statuses.Abstraction;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Business.Services
{
	public class UnitService : BusinessService
	{
		private readonly AppDbContext _db;
		private readonly DocService _docService;
		private readonly PostService _postService;
		private readonly RankPermissionService _rankPermissionService;
		private readonly UnitPermissionService _unitPermissionService;

		public UnitService(
			AppDbContext db,
			DocService docService,
			PostService postService,
			RankPermissionService rankPermissionService,
			UnitPermissionService unitPermissionService,
			ILogger logger
			) : base(logger)
		{
			_db = db;
			_docService = docService;
			_postService = postService;
			_rankPermissionService = rankPermissionService;
			_unitPermissionService = unitPermissionService;
		}

		public async Task<EmptyAction> RegisterAsync(NewUnitDto dto)
		{
			ActionResult<Unit> action = new ActionResult<Unit>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Unit registration restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.RegisterNewUnits)).IsSuccess)
					return action.FormFailure("Unit registration restricted", eventId: EventIds.Forbidden);

				ulong discordId;
				if (!ulong.TryParse(dto.DiscordId, out discordId))
					return action.FormFailure($"Unit registration failed. Incorrect Discord ID", eventId: EventIds.BadInput);

				if ((await _db.Units.FindAsync(discordId)) != null)
					return action.FormFailure($"Unit with ID {discordId} already registered", eventId: EventIds.ImpossibleAction);
				
				action.Value = new Unit
				{
					DiscordId = discordId,
					Nickname = dto.Nickname
				};

				UnitRegistrationEvent registrationEvent = new UnitRegistrationEvent
				{
					Initiator = Actor,
					Units = new List<Unit> { action.Value }
				};

				await _db.Units.AddAsync(action.Value);
				await _db.UnitRegistrationEvents.AddAsync(registrationEvent);
				await _db.SaveChangesAsync();

				action.FormSuccess($"Unit {action.Value.Nickname} registered", eventId: EventIds.Created);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<Unit>> GetAsync(ulong unitId)
		{
			ActionResult<Unit> action = new ActionResult<Unit>(_logger);

			try
			{
				action.Value = await _db.Units.FindAsync(unitId);
				if (action.Value != null)
					action.FormSuccess($"Unit {action.Value.Nickname} with Discord ID {unitId} found", eventId: EventIds.Read);
				else
					action.FormFailure($"Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Получить список абсолютно всех зарегистрированных бойцов
		/// </summary>
		public async Task<ActionResult<List<Unit>>> GetAllUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				action.Value = await _db.Units.ToListAsync();

				action.FormSuccess("All units list formed. Length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Получить всех бойцов, которые на текущий момент в клане
		/// </summary>
		public async Task<ActionResult<List<Unit>>> GetActiveUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				action.Value = await _db.Units.Where(u => u.IsActive()).ToListAsync();

				action.FormSuccess("Active units list formed. Length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Получить список всех уволенных бойцов
		/// </summary>
		public async Task<ActionResult<List<Unit>>> GetDismissedUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				action.Value = await _db.Units
					.Where(u => !u.IsActive())
					.Where(u => !u.IsInRetirement())
					.ToListAsync();

				action.FormSuccess("Dismissed units list formed. Length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Unit>>> GetRetiredUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				action.Value = await _db.Units
					.Where(u => u.IsInRetirement())
					.ToListAsync();

				action.FormSuccess("Units in retirenment list formed. Length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Уволить бойца
		/// </summary>
		public async Task<EmptyAction> DismissAsync(ulong unitId, int? docId = null)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor != null)
				{
					if ((await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.DismissUnits)).IsSuccess)
					{
						if (docId != null)
						{
							_docService.Actor = Actor;
							var docResult = await _docService.GetAsync((int)docId);
							if (!docResult.IsSuccess)
								return action.FormFailure($"Dismissing unit failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
						}

						_logger.LogTrace(EventIds.Processing, $"Searching for unit: {unitId}");
						Unit? unit = await _db.Units.FindAsync(unitId);

						if (unit != null)
						{
							_logger.LogTrace(EventIds.Read, $"Unit found: {unit.ToString()}");
							foreach (AssignedPost assignedPost in unit.GetAssignedPosts())
							{
								_logger.LogTrace(EventIds.Processing, $"Termination AssignedPost: {assignedPost.ToString()}");
								assignedPost.Terminate();
								_logger.LogTrace(EventIds.Updated, $"AssignedPost terminated: {assignedPost.ToString()}");
							}

							foreach (AssignedRank assignedRank in unit.UnitStates.Where(us => us is AssignedRank && us.IsActive()))
							{
								_logger.LogTrace(EventIds.Processing, $"Termination AssignedRank: {assignedRank.ToString()}");
								assignedRank.Terminate();
								_logger.LogTrace(EventIds.Updated, $"AssignedRank terminated: {assignedRank.ToString()}");
							}

							_logger.LogTrace(EventIds.Processing, $"Creating UnitDismissingEvent");
							UnitDismissingEvent dismissingEvent = new UnitDismissingEvent()
							{
								Units = new List<Unit> { unit },
								DocId = docId
							};
							_db.UnitDismissingEvents.Add(dismissingEvent);
							_logger.LogTrace(EventIds.Created, $"Created UnitDismissingEvent: {dismissingEvent.ToString()}");

							_logger.LogTrace(EventIds.Saving, $"Saving changes");
							await _db.SaveChangesAsync();

							action.FormSuccess("Unit dismissed", eventId: EventIds.Updated);
						}
						else
						{
							action.FormFailure("Unit dismissing failed. Unit not found", eventId: EventIds.NotFound);
						}
					}
					else
					{
						action.FormFailure("Unit dismissing restricted", eventId: EventIds.Forbidden);
					}
				}
				else
				{
					action.FormFailure("Unit dismissing restricted. Unauthorized", eventId: EventIds.Unauthorized);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> DismissMultipleAsync(HashSet<ulong> unitIds, int? docId = null)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Dismissing units restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.DismissUnits)).IsSuccess)
					return action.FormFailure("Dismissing units restricted", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Dismissing units failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				uint dismissedCounter = 0u;
				foreach (ulong unitId in unitIds)
				{
					EmptyAction result = await DismissAsync(unitId, docId);
					if (result.IsSuccess)
						dismissedCounter++;
				}

				action.FormSuccess($"Dismissed {dismissedCounter} units", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Отправить бойца в отставку
		/// </summary>
		public async Task<ActionResult<Retirement>> AssignRetirementAsync(ulong unitId, int? docId = null)
		{
			ActionResult<Retirement> action = new ActionResult<Retirement>(_logger);

			try
			{
				if (Actor != null)
				{
					if ((await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignRetirement)).IsSuccess)
					{
						if (docId != null)
						{
							_docService.Actor = Actor;
							var docResult = await _docService.GetAsync((int)docId);
							if (!docResult.IsSuccess)
								return action.FormFailure($"Assigning retirement failed. Doc with ID {docId} not found",
									eventId: EventIds.NotFound);
						}

						_logger.LogTrace(EventIds.Processing, $"Searching for unit: {unitId}");
						Unit? unit = await _db.Units.FindAsync(unitId);

						if (unit != null)
						{
							_logger.LogTrace(EventIds.Read, $"Unit found: {unit.ToString()}");
							foreach (AssignedPost assignedPost in unit.GetAssignedPosts())
							{
								_logger.LogTrace(EventIds.Processing, $"Termination AssignedPost: {assignedPost.ToString()}");
								assignedPost.Terminate();
								_logger.LogTrace(EventIds.Updated, $"AssignedPost terminated: {assignedPost.ToString()}");
							}

							foreach (AssignedRank assignedRank in unit.UnitStates.Where(us => us is AssignedRank && us.IsActive()))
							{
								_logger.LogTrace(EventIds.Processing, $"Termination AssignedRank: {assignedRank.ToString()}");
								assignedRank.Terminate();
								_logger.LogTrace(EventIds.Updated, $"AssignedRank terminated: {assignedRank.ToString()}");
							}

							_logger.LogTrace(EventIds.Processing, $"Creating Retirement state");
							Retirement retirement = new Retirement()
							{
								Unit = unit,
								Start = DateTime.UtcNow,
								DocId = docId
							};
							_db.Retirements.Add(retirement);
							_logger.LogTrace(EventIds.Created, $"Created Retirement state: {retirement.ToString()}");

							_logger.LogTrace(EventIds.Saving, $"Saving changes");
							await _db.SaveChangesAsync();

							action.Value = retirement;
							action.FormSuccess($"Assigned retirement for unit {unit.Nickname}", eventId: EventIds.Updated);
						}
						else
						{
							action.FormFailure($"Assigning retirement failed. Unit with Discord ID {unitId} not found",
								eventId: EventIds.NotFound);
						}
					}
					else
					{
						action.FormFailure("Assigning retirement restricted", eventId: EventIds.Forbidden);
					}
				}
				else
				{
					action.FormFailure("Assigning retirement restricted. Unauthorized", eventId: EventIds.Unauthorized);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Retirement>>> AssignRetirenmentMultipleAsync(HashSet<ulong> unitIds, int? docId = null)
		{
			ActionResult<List<Retirement>> action = new ActionResult<List<Retirement>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Assigning retirements restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignRetirement)).IsSuccess)
					return action.FormFailure("Assigning retirements restricted", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Assigning retirements failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				List<Retirement> retirements = new List<Retirement>();
				foreach (ulong unitId in unitIds)
				{
					ActionResult<Retirement> result = await AssignRetirementAsync(unitId, docId);
					if (result.IsSuccess)
						retirements.Add(result.Value);
				}

				action.FormSuccess($"Assigned retirement for {retirements.Count} units", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Unit>>> ReturnToActiveMultipleAsync(
			HashSet<ulong> unitIds,
			HashSet<int> postIds,
			int rankId,
			int? docId = null)
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Returning inactive units restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignRetirement)).IsSuccess
					&& !(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.DismissUnits)).IsSuccess)
					return action.FormFailure("Returning inactive units restricted", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Returning inactive units failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				Rank? rankToAssign = await _db.Ranks.FindAsync(rankId);
				if (rankToAssign == null)
					return action.FormFailure($"Returning inactive units failed. Rank with ID {rankId} not found", eventId: EventIds.NotFound);

				_postService.Actor = Actor;
				ActionResult<List<Post>> postsResult = await _postService.GetPostsCanAssignAsync();
				if (!postsResult.IsSuccess)
					return action.FormFailure($"Returning inactive units failed. Getting actor's posts can assign failed",
						eventId: EventIds.Failed);

				List<Post> postsToAssign = new List<Post>();

				foreach (int postId in postIds)
				{
					Post? post = postsResult.Value.FirstOrDefault(p => p.Id == postId);
					if (post != null)
					{
						postsToAssign.Add(post);
					}
					else
					{
						_logger.LogWarning(eventId: EventIds.Failed,
							$"Setting returning units to post with ID {postId} failed. Forbidded or not found");
					}
				}

				if (!postsToAssign.Any())
					return action.FormFailure(
						"Returning inactive units failed. There is no posts actor can assign",
						eventId: EventIds.NoData);

				List<Unit> units = new List<Unit>();

				foreach (ulong unitId in unitIds)
				{
					Unit? unit = await _db.Units.FindAsync(unitId);
					if (unit != null)
					{
						if (!unit.IsActive())
						{
							AssignedRank? assignedRank = unit.GetAssignedRank();
							if (assignedRank != null)
								assignedRank.Terminate();

							List<AssignedPost> assignedPosts = unit.GetAssignedPosts();
							foreach (AssignedPost assignedPost in assignedPosts)
								assignedPost.Terminate();

							await _db.AssignedRanks.AddAsync(new AssignedRank
							{
								Unit = unit,
								Rank = rankToAssign,
								DocId = docId
							});

							await _db.AssignedPosts.AddRangeAsync(postsToAssign.Select(p => new AssignedPost
							{
								Unit = unit,
								Post = p,
								DocId = docId
							}));

							Retirement? retirement = unit.GetRetirement();
							if (retirement != null)
								retirement.Terminate();

							units.Add(unit);
						}
						else
						{
							_logger.LogWarning(eventId: EventIds.ImpossibleAction, $"Returning already active unit {unit.Nickname} failed");
						}
					}
					else
					{
						_logger.LogWarning(eventId: EventIds.NotFound, $"Returning unit with Discord ID {unitId} failed. Not found");
					}
				}

				await _db.SaveChangesAsync();

				action.Value = units;
				action.FormSuccess(
					$"Returned {units.Count} to active. " +
					$"Assigned to {rankToAssign.Name} rank and to posts: " +
					String.Join(", ", postsToAssign.Select(p => p.Name))
					);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Status>>> GetUnitStatusesAsync(ulong unitId)
		{
			ActionResult<List<Status>> action = new ActionResult<List<Status>>(_logger);

			try
			{
				Unit? unit = await _db.Units.FindAsync(unitId);
				if (unit == null)
					return action.FormFailure($"Getting unit statuses failed. Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);

				action.Value = unit.GetActiveStatuses();
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<string>>> GetUnitActivityAsync(ulong unitId)
		{
			ActionResult<List<string>> action = new ActionResult<List<string>>(_logger);

			try
			{
				var unit = await _db.Units
					.Include(u => u.Activities)
					.FirstOrDefaultAsync(u => u.DiscordId == unitId);
				if (unit != null)
				{
					action.Value = unit.Activities
						 .OrderBy(a => a.Date)
						.Select(ad => ad.Date.ToString("dd.MM.yyyy"))
						.ToList();
					action.FormSuccess("Unit activity retrieved", eventId: EventIds.Read);
				}
				else
				{
					action.FormFailure("Unit not found", eventId: EventIds.NotFound);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<Activity>> FixActivityAsync(ulong unitId, DateOnly date)
		{
			ActionResult<Activity> action = new ActionResult<Activity>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Activity fixation restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.FixActivity)).IsSuccess)
					return action.FormFailure("Activity fixation restricted", eventId: EventIds.Forbidden);

				Unit? unit = await _db.Units.FindAsync(unitId);
				if (unit == null)
					return action.FormFailure($"Activity fixation failed. Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);

				Activity activity = new Activity
				{
					UnitId = unitId,
					Date = date
				};
				unit.Activities.Add(activity);
				unit.RankUpCounter++;

				action.Value = activity;

				await _db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Activity>>> FixMultipleActivityAsync(HashSet<ulong> unitIds, DateOnly date)
		{
			ActionResult<List<Activity>> action = new ActionResult<List<Activity>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Multiple activity fixation restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.FixActivity)).IsSuccess)
					return action.FormFailure("Multiple activity fixation restricted", eventId: EventIds.Forbidden);

				List<Activity> activities = new List<Activity>();

				foreach (ulong unitId in unitIds)
				{
					Unit? unit = await _db.Units.FindAsync(unitId);
					if (unit == null)
					{
						_logger.LogWarning(eventId: EventIds.NotFound,
							$"Multiple activity fixation failed. Unit with Discord ID {unitId} not found");
						continue;
					}

					Activity activity = new Activity
					{
						UnitId = unitId,
						Date = date
					};
					unit.Activities.Add(activity);
					unit.RankUpCounter++;

					activities.Add(activity);
				}

				await _db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<Status>> ApplyStatusAsync(
			StatusType statusType,
			ulong unitId,
			bool overwrite = false,
			DateTime? end = null,
			int days = 7,
			int? docId = null
			)
		{
			ActionResult<Status> action = new ActionResult<Status>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Applying status restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignStatuses)).IsSuccess)
					return action.FormFailure("Applying status restricted", eventId: EventIds.Forbidden);

				Unit? unit = await _db.Units.FindAsync(unitId);
				if (unit == null)
					return action.FormFailure($"Applying status failed. Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);

				
				DateTime start = DateTime.UtcNow;
				if (end == null)
					end = ((DateTime)start).AddDays(days);

				if (!overwrite)
				{
					Status? currentStatus = unit.GetActiveStatuses().FirstOrDefault();
					int summand = currentStatus != null ? currentStatus.Summand : 0;
					int resultKey = (int)statusType + summand;
					int maxKey = typeof(StatusType).GetEnumValues().Length - 1;
					resultKey = resultKey > maxKey? maxKey : resultKey < 0 ? 0 : resultKey;
					statusType = (StatusType)resultKey;
				}

				Status? newStatus = null;
				switch (statusType)
				{
					case StatusType.Gratitude:
						newStatus = new Gratitude { Unit = unit, Start = start, End = end };
						break;
					case StatusType.Reprimand:
						newStatus = new Reprimand { Unit = unit, Start = start, End = end };
						break;
					case StatusType.SevereReprimand:
						newStatus = new SevereReprimand { Unit = unit, Start = start, End = end };
						break;
					default:
						break;
				}

				if (newStatus != null)
				{
					await _db.Statuses.AddAsync(newStatus);
					await _db.SaveChangesAsync();
					action.Value = newStatus;
					action.FormSuccess($"Set {newStatus.Name} to {unit.Nickname}", eventId: EventIds.Updated);
				}
				else
				{
					action.FormFailure($"Applying status to {unit.Nickname} failed. Invalid status type", eventId: EventIds.HandledError);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Status>>> ApplyStatusMultipleAsync(
			StatusType statusType,
			HashSet<ulong> unitIds,
			bool overwrite = false,
			DateTime? end = null,
			int days = 7,
			int? docId = null
			)
		{
			ActionResult<List<Status>> action = new ActionResult<List<Status>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Applying statuses restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignStatuses)).IsSuccess)
					return action.FormFailure("Applying statuses restricted", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Applying statuses failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				if (end == null)
					end = DateTime.UtcNow.AddDays(days);

				List<Status> assignedStatuses = new List<Status>();
				foreach (ulong unitId in unitIds)
				{
					ActionResult<Status> result = await ApplyStatusAsync(statusType, unitId, overwrite, end, docId: docId);
					if (result.IsSuccess)
						assignedStatuses.Add(result.Value);
				}
				action.Value = assignedStatuses;

				action.FormSuccess($"Applyed statuses to {assignedStatuses.Count} units", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/* Говно галимое. Переделать, пока на сайте не реализовали.
		public async Task<EmptyAction> UpdateAsync(Unit unit)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor != null)
				{
					if (Actor.HasPermission(PermissionType.Administrator))
					{
						if (unit != null)
						{
							_db.Units.Update(unit);
							await _db.SaveChangesAsync();
							action.FormSuccess("Unit updated");
						}
						else
						{
							action.FormFailure("Unit not found");
						}
					}
					else
					{
						action.FormFailure("Unit update restricted");
					}
				}
				else
				{
					action.FormFailure("Unit update restricted. Unauthorized");
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> UpdateUnitStatusAsync(ulong unitId, int statusId)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor != null)
				{
					if (Actor.HasPermission(PermissionType.Administrator))
					{
						var unit = await _db.Units.FindAsync(unitId);
						if (unit != null)
						{
							_db.Units.Update(unit);
							await _db.SaveChangesAsync();
							action.FormSuccess("Unit status updated");
						}
						else
						{
							action.FormFailure("Unit not found");
						}
					}
					else
					{
						action.FormFailure("Unit status update restricted");
					}
				}
				else
				{
					action.FormFailure("Unit status update restricted. Unauthorized");
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> DeleteStatusAsync(int statusId)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				if (Actor != null)
				{
					if (Actor.HasPermission(PermissionType.Administrator))
					{
						var unitStatus = await _db.Statuses.FindAsync(statusId);
						if (unitStatus != null)
						{
							_db.Statuses.Remove(unitStatus);
							await _db.SaveChangesAsync();
							action.FormSuccess("Status deleted", eventId: EventIds.Deleted);
						}
						else
						{
							action.FormFailure("Status not found", eventId: EventIds.NotFound);
						}
					}
					else
					{
						action.FormFailure("Status deletion restricted", eventId: EventIds.Forbidden);
					}
				}
				else
				{
					action.FormFailure("Status deletion restricted. Unauthorized");
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
		*/

		public async Task<ActionResult<Status>> GetUnitStatusAsync(ulong unitId, int statusId)
		{
			ActionResult<Status> action = new ActionResult<Status>(_logger);

			try
			{
				Unit? unit = await _db.Units.FindAsync(unitId);

				if (unit == null)
					action.FormFailure($"Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);

				Status? unitStatus = unit.UnitStates
					.Where(us => us is Status)
					.Select(us => (Status)us)
					.FirstOrDefault(us => us.Id == statusId);
				if (unitStatus == null)
				{
					action.FormFailure($"Status with ID {statusId} not found", eventId: EventIds.NotFound);
				}

				action.FormSuccess("Status retrieved", eventId: EventIds.Read);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<UnitState>>> GetUnitStatesAsync(ulong discordId)
		{
			ActionResult<List<UnitState>> action = new ActionResult<List<UnitState>>(_logger);

			try
			{
				var unit = await _db.Units
					.FindAsync(discordId);

				if (unit != null)
				{
					action.Value = unit.UnitStates.OrderByDescending(us => us.Start).ToList();
					action.FormSuccess($"States history for unit {discordId} formed. Length: " + action.Value.Count,
						eventId: action.Value.Count > 0 ? EventIds.Read : EventIds.NoData);
				}
				else
				{
					action.FormFailure($"Unit with Discord ID {discordId} not found", eventId: EventIds.NotFound);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<SingleDayEvent>>> GetUnitEventsAsync(ulong unitId)
		{
			ActionResult<List<SingleDayEvent>> action = new ActionResult<List<SingleDayEvent>>(_logger);

			try
			{
				var unit = await _db.Units
					.FindAsync(unitId);

				if (unit != null)
				{
					action.Value = unit.SingleDayEvents.OrderByDescending(e => e.DateTime).ToList();
					action.FormSuccess($"Events history for unit {unitId} formed. Length: " + action.Value.Count,
						eventId: action.Value.Count > 0 ? EventIds.Read : EventIds.NoData);
				}
				else
				{
					action.FormFailure($"Unit with Discord ID {unitId} not found", eventId: EventIds.NotFound);
				}
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<FavoriteKit>>> GetAvailableKits()
		{
			ActionResult<List<FavoriteKit>> action = new ActionResult<List<FavoriteKit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Getting available kits failed. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = await _db.FavoriteKits.ToListAsync();

				action.FormSuccess($"Available kits for unit {Actor.Nickname} with Discord ID {Actor.DiscordId} formed. Length: " + action.Value.Count,
						eventId: action.Value.Count > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<BackgroundPicture>>> GetAvailableBackgrounds()
		{
			ActionResult<List<BackgroundPicture>> action = new ActionResult<List<BackgroundPicture>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Getting available backgrounds failed. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = await _db.BackgroundPictures.ToListAsync();

				action.FormSuccess($"Available backgrounds for unit {Actor.Nickname} with Discord ID {Actor.DiscordId} formed. Length: " + action.Value.Count,
						eventId: action.Value.Count > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Unit>>> GetAllNotHeadUnitsAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all not head units. Unauthorized", eventId: EventIds.Unauthorized);

				HashSet<Unit> headUnits = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllHeadsRecursive())
					.SelectMany(p => p.AssignedPosts.Select(ap => ap.Unit))
					.ToHashSet();

				action.Value = await _db.Units.Except(headUnits).ToListAsync();

				action.FormSuccess($"{Actor.Nickname}'s all not head units retrieved. Length: {action.Value.Count}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Unit>>> GetAllHeadUnitsAsync()
		{
			ActionResult<HashSet<Unit>> action = new ActionResult<HashSet<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all head units. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllHeadsRecursive())
					.SelectMany(p => p.AssignedPosts.Select(ap => ap.Unit))
					.ToHashSet();

				action.FormSuccess($"{Actor.Nickname}'s all head units retrieved. Length: {action.Value.Count}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Unit>>> GetAllSubordinateUnitsAsync()
		{
			ActionResult<HashSet<Unit>> action = new ActionResult<HashSet<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all subordinate units. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllSubordinatesRecursive())
					.SelectMany(p => p.AssignedPosts.Select(ap => ap.Unit))
					.ToHashSet();

				action.FormSuccess($"{Actor.Nickname}'s all subordinate units retrieved. Length: {action.Value.Count}",
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
