using Business.Models.Enums;
using Business.Models.SingleDayEvents;
using Business.Models.SingleDayEvents.Abstraction;
using Business.Models.States;
using Business.Models.States.Abstraction;
using Business.Models.Statuses;
using Business.Models.Statuses.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sprache;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Models
{
	public class Unit
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public ulong DiscordId { get; set; }
		public string Nickname { get; set; } = string.Empty;
		public ulong? SteamId { get; set; } = null;
		public int RankUpCounter { get; set; } = 0;
		public Gender Gender { get; set; } = Gender.Male;
		public int FavoriteKitId { get; set; } = 1;
		[JsonIgnore] public virtual FavoriteKit FavoriteKit { get; set; }
		public int BackgroundPictureId { get; set; } = 1;
		[JsonIgnore] public virtual BackgroundPicture BackgroundPicture { get; set; }
		[JsonIgnore] public virtual List<Doc> OwnDocs { get; set; }
		[JsonIgnore] public virtual List<AssignedReward> AssignedRewards { get; set; } = new List<AssignedReward>();
		[JsonIgnore] public virtual List<Activity> Activities { get; set; } = new List<Activity>();
		[JsonIgnore] public virtual List<UnitState> UnitStates { get; set; } = new List<UnitState>();
		[JsonIgnore] public virtual List<SingleDayEvent> SingleDayEvents { get; set; } = new List<SingleDayEvent>();
        
		public UnitRegistrationEvent? GetRegistrationEvent()
		{
			return (UnitRegistrationEvent?)SingleDayEvents.FirstOrDefault(e => e is UnitRegistrationEvent);
		}

		public string GetRegistrationDateTimeString()
		{
			return GetRegistrationEvent()?.DateTime.ToString("dd.MM.yyyy HH:mm") ?? "Неизвестна дата регистрации";
		}

        public AssignedRank? GetAssignedRank(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			return UnitStates
				.Where(us => us is AssignedRank)
				.Select(us => (AssignedRank)us)
				.AsEnumerable()
				.FirstOrDefault(ar => ar.IsActive(dateTime));
		}

		public Rank? GetRank(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			AssignedRank? assignedRank = GetAssignedRank(dateTime);
			return assignedRank == null ? null : assignedRank.Rank;
		}

		public string GetRankName(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			AssignedRank? assignedRank = GetAssignedRank(dateTime);
			return assignedRank == null ? "Без звания" : assignedRank.Rank.Name;
		}

		/// <summary>
		/// Получить максимальное звание которое может получить боец с текущими должностями
		/// </summary>
		/// <returns>Rank, если есть должности. null, если должностей нет</returns>
		public Rank? GetMaxRank()
		{
			return GetPosts().Select(p => p.MaxRank).MinBy(r => r.Index);
		}

		/// <summary>
		/// Получить значение счётчика, при котором бойца нужно будет повысить в звании
		/// </summary>
		/// <returns>int, если есть возможность повыситься, иначе - null</returns>
		/*
		public int? GetCounterToReach()
		{
			Rank? rank = GetRank();
			Rank? maxRank = GetMaxRank();
			if (rank != null && maxRank != null)
			{
				if (rank.Index > maxRank.Index)
				{
					
				}
			}
			return null;
		}

		public string GetRankUpCounterString()
		{
			int? counterToReach = GetCounterToReach();
			if (counterToReach != null)
				return $"{RankUpCounter}/{counterToReach}";
			else
				return RankUpCounter.ToString();
		}
		*/

		public List<AssignedPost> GetAssignedPosts(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			return UnitStates
				.Where(us => us is AssignedPost)
				.Select(us => (AssignedPost)us)
				.Where(ap => ap.IsActive(dateTime))
				.ToList();
		}

		public List<Post> GetPosts(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			return UnitStates
				.Where(us => us is AssignedPost)
				.Select(us => (AssignedPost)us)
				.Where(ap => ap.IsActive(dateTime))
				.Select(ap => ap.Post)
				.ToList();
		}

		public Retirement? GetRetirement(DateTime? dateTime = null)
		{
			dateTime = dateTime ?? DateTime.UtcNow;
			return UnitStates
				.Where(us => us is Retirement)
				.Select(us => (Retirement)us)
				.AsEnumerable()
				.FirstOrDefault(ar => ar.IsActive(dateTime));
		}

		public void CheckRoles()
		{
			Rank? rank = GetRank();
			if (rank != null)
				rank.CheckRoleOnUser(DiscordId);
			foreach (Post post in GetPosts())
				post.CheckRoleOnUser(DiscordId);
		}

		/// <summary>
		/// Проверка, не уволен или не в отставке ли этот боец.
		/// </summary>
		/// <returns>
		/// true, если всё ещё в клане. false, если в отставке или уволен
		/// </returns>
		public bool IsActive()
		{
			return GetRank() != null && GetPosts().Any();
		}

		/// <summary>
		/// Проверка, находится ли боец в отставке
		/// </summary>
		/// <returns>true, если в отставке</returns>
		public bool IsInRetirement()
		{
			return UnitStates.Where(us => us is Retirement).Any(r => r.IsActive());
		}

		public List<Status> GetStatuses()
		{
			return UnitStates.Where(us => us is Status).Select(us => (Status)us).ToList();
		}

		public List<Status> GetActiveStatuses()
		{
			return UnitStates.Where(us => us is Status && us.IsActive()).Select(us => (Status)us).ToList();
		}

		public Post? GetHighestPost()
		{
			Post? highestPost = null;
			List<Post> higherPosts = new List<Post>();
			foreach (Post post in GetPosts())
			{
				if (highestPost != null)
				{
					if (!higherPosts.Contains(post))
						continue;
				}
				highestPost = post;
				higherPosts = post.GetAllHeadsRecursive();
			}
			return highestPost;
		}

		public bool IsMale()
		{
			return Gender == Gender.Male;
		}

		public bool IsFemale()
		{
			return Gender == Gender.Female;
		}

		/// <summary>
		/// Выдаёт список активности, которая была зафиксирована на этой неделе, начиная с понедельника включительно
		/// </summary>
		public List<Activity> GetWeekActivity()
		{
			int dayOfWeekNum = DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)DateTime.UtcNow.DayOfWeek - 1;
			DateOnly startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-dayOfWeekNum));
			return Activities.Where(a => a.Date >= startDate).ToList();
		}

		/// <summary>
		/// Выдаёт список активности, которая была зафиксирована в этом месяце, начиная с первого числа месяца включительно
		/// </summary>
		public List<Activity> GetMonthActivity()
		{
			DateTime now = DateTime.UtcNow;
			DateOnly startDate = new DateOnly(now.Year, now.Month, 1);
			return Activities.Where(a => a.Date >= startDate).ToList();
		}

        /// <summary>
        /// Выдаёт список активности, которая была зафиксирована в этом году, начиная с первого января включительно
        /// </summary>
        public List<Activity> GetYearActivity()
		{
			DateTime now = DateTime.UtcNow;
			DateOnly startDate = new DateOnly(now.Year, 1, 1);
			return Activities.Where(a => a.Date >= startDate).ToList();
		}

		public int? GetRankIndex()
		{
			return GetRank()?.Index;
        }

		public int? GetPostIndex()
		{
			return GetHighestPost()?.GetIndex();
		}

		public UnitDto ToDto()
		{
			return new UnitDto
			{
				DiscordId = DiscordId.ToString(),
				Nickname = Nickname,
				SteamId = SteamId.ToString() ?? "",
				RankUpCounter = RankUpCounter,
				Joined = GetRegistrationDateTimeString(),
				Gender = (int)Gender,
				RankIndex = GetRankIndex(),
				PostIndex = GetPostIndex(),
				RankId = GetRank()?.Id,
				BackgroundPicture = BackgroundPicture,
				FavoriteKit = FavoriteKit,
				PostsIds = GetPosts().OrderByDescending(p => p.GetIndex()).Select(p => p.Id).ToList(),
				WeekActivityCount = GetWeekActivity().Count,
				MonthActivityCount = GetMonthActivity().Count,
				YearActivityCount = GetYearActivity().Count,
				TotalActivityCount = Activities.Count,
				AssignedRewards = AssignedRewards
			};
		}

		public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
	}

	/// <summary>
	/// DTO бойца на экспорт с бэкенда бизнес-логики
	/// </summary>
	public class UnitDto
	{
        public string DiscordId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
        public int RankUpCounter { get; set; } = 0;
        public string Joined { get; set; } = string.Empty;
        public int Gender { get; set; }
		public int? RankIndex { get; set; } = 0;
		public int? PostIndex { get; set; } = 0;
		public int WeekActivityCount { get; set; }
		public int MonthActivityCount { get; set; }
		public int YearActivityCount { get; set; }
		public int TotalActivityCount { get; set; }
        public BackgroundPicture BackgroundPicture { get; set; }
        public FavoriteKit FavoriteKit { get; set; }
        public int? RankId { get; set; }
        public List<int> PostsIds { get; set; } = new();
        public List<AssignedReward> AssignedRewards { get; set; } = new();

        public override string ToString()
		{
			return JsonSerializer.Serialize(this);
		}
	}

	/// <summary>
	/// Получаемый с внешних сервисов DTO, предназначеный
	/// для добавления новых бойцов в базу данных.
	/// </summary>
	public class NewUnitDto
	{
		public string DiscordId { get; set; }
		public string Nickname { get; set; }
		public int? PostsIds { get; set; } = null;
		public int? RankId { get; set; } = null;
		public int? RewardId { get; set; } = null;

		public override string ToString()
		{
			return JsonSerializer.Serialize(this);
		}
	}

	public class UnitConfiguration : IEntityTypeConfiguration<Unit>
	{
		public void Configure(EntityTypeBuilder<Unit> builder)
		{
			builder.HasMany(u => u.OwnDocs).WithOne(d => d.Author).OnDelete(DeleteBehavior.SetNull);
			builder.HasMany(u => u.AssignedRewards).WithOne(ar => ar.Unit).OnDelete(DeleteBehavior.Cascade);
			builder.HasMany(u => u.Activities).WithOne(a => a.Unit).OnDelete(DeleteBehavior.Cascade);
			builder.HasMany(u => u.UnitStates).WithOne(us => us.Unit);
			builder.HasMany(u => u.SingleDayEvents).WithMany(e => e.Units);
		}
	}
}