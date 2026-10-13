using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.Statuses;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Business.Services
{
    public class PostService : BusinessService
    {
		private readonly ILogger<PostService> _logger;
		private readonly AppDbContext _db;
		private readonly DocService _docService;
		private readonly RankService _rankService;
		private readonly SubdivisionService _subdivisionService;
		private readonly UnitPermissionService _unitPermissionService;

        public PostService(
			AppDbContext db,
			DocService docService,
			RankService rankService,
			SubdivisionService subdivisionService,
			UnitPermissionService unitPermissionService,
			ILogger<PostService> logger)
        {
            _db = db;
			_docService = docService;
			_rankService = rankService;
			_subdivisionService = subdivisionService;
			_unitPermissionService = unitPermissionService;
			_logger = logger;
		}

        public async Task<ActionResult<Post>> CreateAsync(
            string name,
            string description,
            int? subdivisionId,
            int? headId,
            int maxRankId,
            string color,
            bool appendSubdivisionName
            )
        {
            ActionResult<Post> action = new ActionResult<Post>(_logger);

            try
            {
				if (headId != null)
				{
					ActionResult<Post> headResult = await CheckCanManagePostAsync((int)headId);
					if (!headResult.IsSuccess)
						return action.FormFailure("Creating post restricted. Permission check failed", eventId: EventIds.Forbidden);
				}
				else
				{
					if (Actor == null)
						return action.FormFailure("Creating post restricted. Unauthorized", eventId: EventIds.Unauthorized);
					if (!(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess)
						return action.FormFailure("Creating post restricted. Can't create post without head", eventId: EventIds.Forbidden);
				}

				if (subdivisionId != null)
				{
					_subdivisionService.Actor = Actor;
					ActionResult<Subdivision> subdivResult = await _subdivisionService.CheckCanManageSubdivisionAsync((int)subdivisionId);
					if (!subdivResult.IsSuccess)
						return action.FormFailure($"Can't assign post to subdivision with ID {subdivisionId}", eventId: EventIds.Forbidden);
				}

				Rank? rank = Actor.GetMaxRank();
				if (rank == null)
					return action.FormFailure("Post creation failed. Can't get user's max available post", eventId: EventIds.NotFound);
				
				ActionResult<Rank> rankResult = await _rankService.GetAsync(maxRankId);
				if (!rankResult.IsSuccess)
					return action.FormFailure($"Post creation failed. Rank with ID {maxRankId} not found", eventId: EventIds.NotFound);

				if (rankResult.Value.Index < rank.Index)
					return action.FormFailure($"Post creation failed. Max rank is higher, than user's max rank", eventId: EventIds.Forbidden);

				action.Value = new Post
				{
					Name = name,
					Description = description,
					SubdivisionId = subdivisionId,
					HeadId = headId,
					MaxRankId = maxRankId,
					Color = color,
					AppendSubdivisionName = appendSubdivisionName
				};

				action.Value.UpdateRole();

				await _db.Posts.AddAsync(action.Value);
				await _db.SaveChangesAsync();

				action.FormSuccess($"Post {action.Value.GetFullName()} with ID {action.Value.Id} created", eventId: EventIds.Created);
			}
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
		}

        public async Task<ActionResult<Post>> GetAsync(int postId)
        {
			ActionResult<Post> action = new ActionResult<Post>(_logger);

            try
            {
				action.Value = await _db.Posts.FindAsync(postId);

				if (action.Value != null)
					action.FormSuccess($"Getting post succeed. Post {action.Value} with ID {postId} found", eventId: EventIds.Read);
				else
					action.FormFailure($"Getting post failed. Post with ID {postId} not found", eventId: EventIds.NotFound);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
            }

            return action;
		}

        public async Task<ActionResult<List<Post>>> GetAllAsync()
        {
            ActionResult<List<Post>> action = new ActionResult<List<Post>>(_logger);

			try
			{
				action.Value = await _db.Posts.ToListAsync();

				action.FormSuccess("Posts list formed, length: " + action.Value.Count(),
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
        }

		public async Task<EmptyAction> UpdateAsync(
			int postId,
			string name,
			bool appendSubdivisionName,
			string description,
			string color,
			int? subdivisionId,
			int maxRankId,
			int? headId
			)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				ActionResult<Post> postResult = await CheckCanManagePostAsync(postId);
				if (!postResult.IsSuccess)
					return action.FormFailure("Updating post restricted. Permission check failed", eventId: EventIds.Forbidden);

				Post? headPost = null;

				if (headId != null)
				{
					ActionResult<Post> headResult = await CheckCanManagePostAsync((int)headId);
					if (!headResult.IsSuccess)
						return action.FormFailure($"Updating post restricted. Can't set head with ID {headId}", eventId: EventIds.Forbidden);
					headPost = headResult.Value;
				}
				else
				{
					if (Actor == null)
						return action.FormFailure("Updating post restricted. Unauthorized", eventId: EventIds.Unauthorized);
					if (!(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess)
						return action.FormFailure("Updating post restricted. Can't set post's head to null", eventId: EventIds.Forbidden);
				}

				if (subdivisionId != null)
				{
					_subdivisionService.Actor = Actor;
					ActionResult<Subdivision> subdivResult = await _subdivisionService.CheckCanManageSubdivisionAsync((int)subdivisionId);
					if (!subdivResult.IsSuccess)
						return action.FormFailure($"Can't assign post {postResult.Value.Name} with ID {postId}" +
							$" to subdivision with ID {subdivisionId}", eventId: EventIds.Forbidden);
				}

				Rank? availableMaxRank = null;
				if (headPost == null)
				{
					availableMaxRank = Actor.GetMaxRank();
					if (availableMaxRank == null)
						return action.FormFailure($"Post {postResult.Value.Name} with ID {postId} updating failed. " +
							$"Can't get user's max available rank", eventId: EventIds.HandledError);
				}
				else
				{
					availableMaxRank = headPost.MaxRank;
				}

				ActionResult<Rank> rankResult = await _rankService.GetAsync(maxRankId);
				if (!rankResult.IsSuccess)
					return action.FormFailure($"Post {postResult.Value.Name} with ID {postId} updating failed. " +
						$"Rank with ID {maxRankId} not found", eventId: EventIds.NotFound);

				if (rankResult.Value.Index < availableMaxRank.Index)
					return action.FormFailure($"Post {postResult.Value.Name} with ID {postId} updating failed. " +
						$"Rank {rankResult.Value.Name} is higher, " +
						$"than unit's {Actor.Nickname} max available rank {availableMaxRank.Name}", eventId: EventIds.Forbidden);

				postResult.Value.Name = name;
				postResult.Value.Description = description;
				postResult.Value.SubdivisionId = subdivisionId;
				postResult.Value.HeadId = headId;
				postResult.Value.MaxRankId = maxRankId;
				postResult.Value.Color = color;
				postResult.Value.AppendSubdivisionName = appendSubdivisionName;

				postResult.Value.UpdateRole();

				_db.Posts.Update(postResult.Value);
				await _db.SaveChangesAsync();

				action.FormSuccess($"Post {postResult.Value.Name} with ID {postId} updated", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Устанавливает должности разрешения по переданным permission ID.
		/// Перезаписывает только разрешения, выданные конкретно этой должности,
		/// а не унаследованные разрешения от подчинённых.
		/// Попытка снять или установить разрешение, которого нет у пользователя
		/// будет проигнорированна.
		/// </summary>
		public async Task<EmptyAction> UpdatePermissionsAsync(int postId, List<GivePermissionDto> permissionDtos)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				ActionResult<Post> postResult = await CheckCanManagePostAsync(postId);
				if (!postResult.IsSuccess)
					return action.FormFailure("Updating post permissions restricted. Permission check failed", eventId: EventIds.Forbidden);

				List<GivedPermission<Post>> givedPermissions = postResult.Value.GivedPermissions.ToList();
				int permissionsHad = givedPermissions.Count;

				foreach (GivedPermission<Post> givedPermission in givedPermissions)
				{
					if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, givedPermission.PermissionType)).IsSuccess)
						_db.PostPermissions.Remove(givedPermission);
				}

				foreach (GivePermissionDto permissionDto in permissionDtos)
				{
					if (permissionDto.PermissionId > 0 && permissionDto.PermissionId <= typeof(PermissionType).GetEnumValues().Length)
					{
						PermissionType permissionType = (PermissionType)permissionDto.PermissionId;
						if ((await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, permissionType)).IsSuccess && !postResult.Value.HasPermission(permissionType))
						{
							Permission? permission = await _db.Permissions.FindAsync(permissionType);
							if (permission != null)
							{
								await _db.PostPermissions.AddAsync(new GivedPermission<Post>
								{
									Permission = permission,
									Inherit = permissionDto.Inherit,
									EntityId = postId
								});
							}
						}
					}
				}

				await _db.SaveChangesAsync();

				action.FormSuccess($"Post {postResult.Value.Name} with ID {postId} permissions updated." +
					$"Then {permissionsHad}, now {postResult.Value.GivedPermissions.Count}", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/// <summary>
		/// Обновляет Discord роль должности или создаёт её, если не существует
		/// </summary>
		/// <returns>ActionResult с Discord ID роли должности</returns>
		public async Task<ActionResult<ulong?>> UpdateRoleAsync(int postId)
		{
			ActionResult<ulong?> action = new ActionResult<ulong?>(_logger);

			try
			{
				ActionResult<Post> result = await CheckCanManagePostAsync(postId);

				if (!result.IsSuccess)
					return action.FormFailure("Post's role updating restricted. Permission check failed", eventId: EventIds.Forbidden);

				result.Value.UpdateRole();
				action.Value = result.Value.DiscordRoleId;

				await _db.SaveChangesAsync();

				action.FormSuccess($"Post {result.Value.GetFullName()} Discord role updated", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Unit>>> GetUnitsByPostAsync(int postId)
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				Post? post = await _db.Posts.FindAsync(postId);
				if (post == null)
					return action.FormFailure($"Getting units by post failed. Post with ID {postId} not found", eventId: EventIds.NotFound);

				action.Value = post.AssignedPosts.Where(r => r.IsActive()).Select(ar => ar.Unit).ToList();

				action.FormSuccess($"Units by {post.Name} post retrieved",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<EmptyAction> DeleteAsync(int postId)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				ActionResult<Post> result = await CheckCanManagePostAsync(postId);

				if (!result.IsSuccess)
					return action.FormFailure("Deleting post restricted. Permission check failure", eventId: EventIds.Forbidden);

				_db.Posts.Remove(result.Value);

				await _db.SaveChangesAsync();

				action.FormSuccess($"Post {result.Value.Name} deleted", eventId: EventIds.Deleted);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<AssignedPost>>> AssignMultipleAsync(HashSet<ulong> unitIds, HashSet<int> postIds, bool overwrite = false, int? docId = null)
		{
			ActionResult<List<AssignedPost>> action = new ActionResult<List<AssignedPost>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Posts assigning restricted. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignPosts)).IsSuccess)
					return action.FormFailure("Posts assigning restricted", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Assigning rewards failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				var canAssignResult = await GetPostsCanAssignAsync();

				if (!canAssignResult.IsSuccess)
					return action.FormFailure("Posts assigning failed. Unknown handled error", eventId: EventIds.HandledError);

				List<Post> canChange = canAssignResult.Value;
				if (!canChange.Any())
					return action.FormFailure("Posts assigning failed. Can't set any currentPosts", eventId: EventIds.Failed);

				action.Value = new List<AssignedPost>();
				List<Unit> units = new List<Unit>();

				foreach (ulong unitId in unitIds)
				{
					Unit? unit = await _db.Units.FindAsync(unitId);
					if (unit == null)
					{
						_logger.LogWarning(eventId: EventIds.NotFound,
							$"Posts assigning failed. Unit with ID {unitId} not found");
						continue;
					}
					if (!unit.IsActive() && !(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess)
					{
						_logger.LogWarning(eventId: EventIds.Forbidden,
							$"Posts assigning failed. Unit {unit.Nickname} is in retirement or dismissed");
						continue;
					}
					units.Add(unit);
				}

				List<Post> posts = new List<Post>();

				foreach (int postId in postIds)
				{
					Post? post = await _db.Posts.FindAsync(postId);
					if (post == null)
					{
						_logger.LogWarning(eventId: EventIds.NotFound, $"Post assigning failed. Post with ID {postId} not found");
						continue;
					}
					if (!canChange.Contains(post))
					{
						_logger.LogWarning(
							eventId: EventIds.Forbidden,
							$"Post assigning restricted. Actor {Actor.Nickname} with Discord ID " +
							$"{Actor.DiscordId} cannot assign post {post.GetFullName()} with ID {postId}");
						continue;
					}
					posts.Add(post);
				}

				foreach (Unit unit in units) {
					List<Post> currentPosts = unit.GetPosts();
					HashSet<Post> postsToKeep = new HashSet<Post>();
					HashSet<Post> postsToAssign = new HashSet<Post>();
					HashSet<Post> postsToDepose = new HashSet<Post>();
					HashSet<Post> failedToDepose = new HashSet<Post>();

					foreach (Post post in posts)
					{
						if (currentPosts.Contains(post))
						{
							postsToKeep.Add(post);
							_logger.LogDebug(eventId: EventIds.Processing,
								$"Posts assigning in process. Post {post.Name} with ID {post.Id} keeped.");
							continue;
						}
						postsToAssign.Add(post);
					}

					if (overwrite)
					{
						List<AssignedPost> assignedPosts = unit.GetAssignedPosts();
						foreach (AssignedPost assignedPost in assignedPosts)
						{
							if (postsToKeep.Contains(assignedPost.Post))
								continue;
							if (!canChange.Contains(assignedPost.Post))
							{
								_logger.LogWarning(
									eventId: EventIds.Forbidden,
									$"Post deposing restricted. Actor {Actor.Nickname} with Discord ID {Actor.DiscordId}" +
									$" cannot depose from post {assignedPost.Post.GetFullName()} with ID {assignedPost.Post.Id}");
								failedToDepose.Add(assignedPost.Post);
								continue;
							}
							postsToDepose.Add(assignedPost.Post);
						}

						int postsInResult = postsToKeep.Count() + postsToAssign.Count() - postsToDepose.Count();
						if (postsInResult < 1)
						{
							_logger.LogDebug(eventId: EventIds.ImpossibleAction,
								$"Posts assigning failed. Posts in postResult cannot be under 1 ({postsInResult})");
							continue;
						}

						foreach (AssignedPost assignedPost in assignedPosts.IntersectBy(postsToDepose, ap => ap.Post))
						{
							assignedPost.Terminate();
						}
					}

					DateTime start = DateTime.UtcNow;

					foreach (Post postToAssign in postsToAssign)
					{
						AssignedPost assignedPost = new AssignedPost()
						{
							Post = postToAssign,
							Start = start,
							DocId = docId,
							Unit = unit
						};
						await _db.AssignedPosts.AddAsync(assignedPost);
						action.Value.Add(assignedPost);
					}
				}

				await _db.SaveChangesAsync();

				action.FormSuccess($"Formed {action.Value.Count} post assignments to {units.Count} units", eventId: EventIds.Updated);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/* На некоторое время оставляем только метод множественного назначения AssignMultipleAsync
		public async Task<ActionResult<AssignedPost>> AssignPostAsync(ulong unitDiscordId, int postId, Unit? unit = null, int? docId = null)
		{
			ActionResult<AssignedPost> action = new ActionResult<AssignedPost>(_logger);

			try
			{
				ActionResult<Post> result = await CheckCanAssignPostAsync(postId);
				if (!result.IsSuccess)
					return action.FormFailure("PostId assigning restricted. Permission check failed", eventId: EventIds.Forbidden);

				if (docId != null)
				{
					_docService.Actor = Actor;
					var docResult = await _docService.GetAsync((int)docId);
					if (!docResult.IsSuccess)
						return action.FormFailure($"Assigning rewards failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

				if (unit == null)
					unit = await _db.Units.FindAsync(unitDiscordId);
				if (unit == null)
					return action.FormFailure($"PostId assigning failed. Unit with ID {unitDiscordId} not found", eventId: EventIds.NotFound);

                AssignedPost assignedPost = new AssignedPost
				{
					Unit = unit,
					Post = result.Value,
					DocId = docId
				};
				action.Value = assignedPost;

				await _db.AssignedPosts.AddAsync(assignedPost);
				await _db.SaveChangesAsync();

				action.FormSuccess($"Unit {unit.Nickname} was assigned to post {result.Value.GetFullName()}", eventId: EventIds.Created);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
		*/

		public async Task<EmptyAction> DeposeAsync(ulong unitDiscordId, int postId, Unit? unit = null, AssignedPost? assignedPost = null)
		{
			EmptyAction action = new EmptyAction(_logger);

			try
			{
				Post? post = null;
				if (assignedPost != null)
					post = assignedPost.Post;
				EmptyAction result = await CheckCanAssignPostAsync(postId, post);
				if (!result.IsSuccess)
					return action.FormFailure("Unit deposing restricted. Permission check failure", eventId: EventIds.Forbidden);

				if (unit == null)
					unit = await _db.Units.FindAsync(unitDiscordId);
				if (unit == null)
					return action.FormFailure($"Unit deposing failed. Unit with Discord ID {unitDiscordId} not found", eventId: EventIds.NotFound);
				if (!unit.IsActive())
					return action.FormFailure($"Unit {unit.Nickname} deposing impossible. Unit is in retirement or dismissed", eventId: EventIds.ImpossibleAction);
				if (unit.GetPosts().Count == 0)
					return action.FormFailure($"Unit {unit.Nickname} deposing failed. Unit have no assigned postsToAssign", eventId: EventIds.ImpossibleAction);
				if (unit.GetPosts().Count == 1)
					return action.FormFailure($"Unit {unit.Nickname} deposing failed. Unit has last post assigned", eventId: EventIds.ImpossibleAction);
				
				if (assignedPost == null)
					assignedPost = await _db.AssignedPosts.FirstOrDefaultAsync(ap => ap.UnitId == unit.DiscordId && ap.PostId == postId && ap.IsActive(null));
				if (assignedPost == null)
					return action.FormFailure($"Unit {unit.Nickname} deposing failed. Unit isn't assigned to post with ID {postId}", eventId: EventIds.NotFound);
				
				assignedPost.Terminate();
				await _db.SaveChangesAsync();

				action.FormSuccess($"Unit {unit.Nickname} deposed from {assignedPost.Post.Name}", eventId: EventIds.Ok);
			}
			catch (Exception ex)
			{
				return action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<Post>> CheckCanManagePostAsync(int postId, Post? post = null)
		{
			ActionResult<Post> action = new ActionResult<Post>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't check permissions. Unauthorized", eventId: EventIds.Unauthorized);
				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.ManageStructure)).IsSuccess)
					return action.FormFailure($"{Actor.Nickname} doesn't have ManageStructure permission", eventId: EventIds.Forbidden);

				if (post == null)
					post = await _db.Posts.FindAsync(postId);
				action.Value = post;

				if (action.Value == null)
					return action.FormFailure($"Can't check permissions. PostId with ID {postId} not found", eventId: EventIds.NotFound);

				List<Post> actorControllablePosts = Actor.GetPosts().SelectMany(p => p.GetAllSubordinatesRecursive()).ToList();
				
				if (!(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess && !actorControllablePosts.Contains(action.Value))
					return action.FormFailure($"PostId {action.Value.GetFullName()} isn't under {Actor.Nickname}'s control", eventId: EventIds.Forbidden);

				action.FormSuccess($"{Actor.Nickname} can manage post {action.Value.GetFullName()}");
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<Post>> CheckCanAssignPostAsync(int postId, Post? post = null)
		{
			ActionResult<Post> action = new ActionResult<Post>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't check post assignment permissions." +
						" Unauthorized", eventId: EventIds.Unauthorized);

				if (post == null)
					post = await _db.Posts.FindAsync(postId);
				action.Value = post;

				if (action.Value == null)
					return action.FormFailure($"Can't check post assignment permissions." +
						$" PostId with ID {postId} not found", eventId: EventIds.NotFound);

				if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignPosts)).IsSuccess)
					return action.FormFailure($"{Actor.Nickname} don't have AssignMultipleAsync permission", eventId: EventIds.Forbidden);

				List<Post> actorHeads = Actor.GetPosts().SelectMany(p => p.GetAllHeadsRecursive()).ToList();

				if (!(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess && actorHeads.Contains(action.Value))
					return action.FormFailure(
						$"PostId {action.Value.GetFullName()} is one of {Actor.Nickname}'s heads", eventId: EventIds.Forbidden);

				action.FormSuccess($"{Actor.Nickname} can assign post {action.Value.GetFullName()}", eventId: EventIds.Accessed);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Post>>> GetPostsCanAssignAsync()
		{
			ActionResult<List<Post>> action = new ActionResult<List<Post>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Getting postsToAssign can assign failed. Unauthorized", eventId: EventIds.Unauthorized);

				if ((await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.AssignPosts)).IsSuccess)
					return action.FormFailure($"{Actor.Nickname} don't have AssignMultipleAsync permission", eventId: EventIds.Forbidden);

				if ((await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess)
				{
					action.Value = await _db.Posts.ToListAsync();
				}
				else
				{
					ActionResult<List<Post>> result = await GetAllNotHeadPostsAsync();
					if (!result.IsSuccess)
						return action.FormFailure("Getting postsToAssign can assign failed." +
							" Handled error in getting all not head postsToAssign", eventId: EventIds.HandledError);
					action.Value = result.Value;
				}

				action.FormSuccess($"{Actor.Nickname}'s postsToAssign can assign retrieved. Length: {action.Value.Count()}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		/*
		 * Какая-то чебухня. Не знаю, зачем я это накодил. Оно не корректно из-за текущей логики назначения должностей.
		 * Правильнее будет проверять именно по должностям, которые может снимать пользователь.
		public async Task<ActionResult<List<Unit>>> GetUnitsCanDeposeAsync()
		{
			ActionResult<List<Unit>> action = new ActionResult<List<Unit>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Getting units can change postsToAssign failed. Unauthorized", eventId: EventIds.Unauthorized);

				if (!Actor.HasPermission(PermissionType.AssignMultipleAsync))
					return action.FormFailure($"{Actor.Nickname} don't have AssignMultipleAsync permission", eventId: EventIds.Forbidden);

				if (Actor.IsAdmin())
				{
					action.Value = await _db.Units.ToListAsync();
				}
				else
				{
					_unitService.Actor = Actor;
					ActionResult<List<Unit>> postResult = await _unitService.GetAllNotHeadUnitsAsync();
					if (!postResult.IsSuccess)
						return action.FormFailure("Getting postsToAssign can assign failed." +
							" Handled error in getting all not head units", eventId: EventIds.HandledError);
					action.Value = postResult.Value;
				}

				action.FormSuccess($"{Actor.Nickname}'s postsToAssign can assign retrieved. Length: {action.Value.Count()}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}
		*/

		public async Task<ActionResult<HashSet<Post>>> GetAllHeadPostsAsync()
		{
			ActionResult<HashSet<Post>> action = new ActionResult<HashSet<Post>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all head postsToAssign. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllHeadsRecursive())
					.ToHashSet();

				action.FormSuccess($"{Actor.Nickname}'s all head postsToAssign retrieved. Length: {action.Value.Count}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<HashSet<Post>>> GetAllSubordinatePostsAsync()
		{
			ActionResult<HashSet<Post>> action = new ActionResult<HashSet<Post>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all subordinate postsToAssign. Unauthorized", eventId: EventIds.Unauthorized);

				action.Value = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllSubordinatesRecursive())
					.ToHashSet();

				action.FormSuccess($"{Actor.Nickname}'s all not subordinate retrieved. Length: {action.Value.Count}",
					eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

		public async Task<ActionResult<List<Post>>> GetAllNotHeadPostsAsync()
		{
			ActionResult<List<Post>> action = new ActionResult<List<Post>>(_logger);

			try
			{
				if (Actor == null)
					return action.FormFailure("Can't get all not head postsToAssign. Unauthorized", eventId: EventIds.Unauthorized);

				HashSet<Post> headPosts = Actor
					.GetPosts()
					.SelectMany(p => p.GetAllHeadsRecursive())
					.ToHashSet();

				action.Value = await _db.Posts.Except(headPosts).ToListAsync();

				action.FormSuccess($"{Actor.Nickname}'s all not head postsToAssign retrieved. Length: {action.Value.Count}",
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
