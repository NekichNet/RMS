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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;

namespace Business.Database
{
	public class AppDbContext : DbContext
	{
        public DbSet<Unit> Units { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Rank> Ranks { get; set; }
        public DbSet<Subdivision> Subdivisions { get; set; }

        public DbSet<Permission> Permissions { get; set; }
        public DbSet<GivedPermission<Post>> PostPermissions { get; set; }
		public DbSet<GivedPermission<Rank>> RankPermissions { get; set; }
		public DbSet<GivedPermission<Subdivision>> SubdivisionPermissions { get; set; }

		public DbSet<Reward> Rewards { get; set; }
        public DbSet<AssignedReward> AssignedRewards { get; set; }

        public DbSet<FavoriteKit> FavoriteKits { get; set; }
        public DbSet<BackgroundPicture> BackgroundPictures { get; set; }

        public DbSet<Doc> Docs { get; set; }
        public DbSet<Activity> Activities { get; set; }

        public DbSet<UnitState> UnitStates { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<Gratitude> Gratitudes { get; set; }
        public DbSet<Reprimand> Reprimands { get; set; }
        public DbSet<SevereReprimand> SevereReprimands { get; set; }
        public DbSet<AssignedPost> AssignedPosts { get; set; }
        public DbSet<AssignedRank> AssignedRanks { get; set; }
        public DbSet<Retirement> Retirements { get; set; }

        public DbSet<SingleDayEvent> SingleDayEvents { get; set; }
        public DbSet<EventWithDoc> EventsWithDoc { get; set; }
        public DbSet<CustomEvent> CustomEvents { get; set; }
        public DbSet<CustomEventWithDoc> CustomEventsWithDoc { get; set; }
        public DbSet<RewardAssignmentEvent> RewardAssignmentEvents { get; set; }
        public DbSet<UnitRegistrationEvent> UnitRegistrationEvents { get; set; }
        public DbSet<UnitDismissingEvent> UnitDismissingEvents { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseLazyLoadingProxies()
                .ConfigureWarnings(warnings => warnings
                    .Ignore(RelationalEventId.ForeignKeyPropertiesMappedToUnrelatedTables)
                    .Ignore(CoreEventId.ForeignKeyAttributesOnBothNavigationsWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
			modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

			/* 1. Разрешения */

			List<Permission> permissions = new List<Permission>();
            foreach (var permissionType in typeof(PermissionType).GetEnumValues())
            {
                var fieldInfo = typeof(PermissionType).GetField(permissionType.ToString());
                if (fieldInfo != null)
                {
                    foreach (Attribute attribute in fieldInfo.GetCustomAttributes(false))
                    {
                        if (attribute is PermissionAttribute permissionAttribute)
                        {
                            permissions.Add(new Permission()
                            {
                                Type = (PermissionType)permissionType,
                                Name = permissionAttribute.Name,
                                Description = permissionAttribute.Description
                            });
                        }
                    }
                }
            }
            modelBuilder.Entity<Permission>().HasData(permissions);
            

			/* 2. Звания */
			List<Rank> ranks = new List<Rank>()
            {
                new Rank { Id = 1, Name = "Рекрут", CounterToReach = 0, Color = "#098100", HigherId = 2 },
                new Rank { Id = 2, Name = "Рядовой", CounterToReach = 7, Color = "#098100", LowerId = 1, HigherId = 3 },
                new Rank { Id = 3, Name = "Ефрейтор", CounterToReach = 3, Color = "#098100", LowerId = 2, HigherId = 4 },
                new Rank { Id = 4, Name = "Младший Сержант", CounterToReach = 3, Color = "#0ba300", LowerId = 3, HigherId = 5 },
                new Rank { Id = 5, Name = "Сержант", CounterToReach = 5, Color = "#0ba300", LowerId = 4, HigherId = 6 },
                new Rank { Id = 6, Name = "Старший Сержант", CounterToReach = 5, Color = "#0ba300", LowerId = 5, HigherId = 7 },
                new Rank { Id = 7, Name = "Старшина", CounterToReach = 5, Color = "#0ba300", LowerId = 6, HigherId = 8 },
                new Rank { Id = 8, Name = "Прапорщик", CounterToReach = 7, Color = "#0ba300", LowerId = 7, HigherId = 9 },
                new Rank { Id = 9, Name = "Старший Прапорщик", CounterToReach = 7, Color = "#0ba300", LowerId = 8, HigherId = 10 },
                new Rank { Id = 10, Name = "Младший Лейтенант", CounterToReach = 7, Color = "#00db3a", LowerId = 9, HigherId = 11 },
                new Rank { Id = 11, Name = "Лейтенант", CounterToReach = 7, Color = "#00db3a", LowerId = 10, HigherId = 12 },
                new Rank { Id = 12, Name = "Старший Лейтенант", CounterToReach = 7, Color = "#00db3a", LowerId = 11, HigherId = 13 },
                new Rank { Id = 13, Name = "Капитан", CounterToReach = 10, Color = "#00db3a", LowerId = 12, HigherId = 14 },
                new Rank { Id = 14, Name = "Майор", CounterToReach = 20, Color = "#00ff88", LowerId = 13, HigherId = 15 },
                new Rank { Id = 15, Name = "Подполковник", CounterToReach = 30, Color = "#00ff88", LowerId = 14, HigherId = 16 },
                new Rank { Id = 16, Name = "Полковник", CounterToReach = 30, Color = "#00ff88", LowerId = 15, HigherId = 17 },
                new Rank { Id = 17, Name = "Генерал-Майор", CounterToReach = 30, Color = "#00ffc0", LowerId = 16, HigherId = 18 },
                new Rank { Id = 18, Name = "Генерал-Лейтенант", CounterToReach = 30, Color = "#00ffc0", LowerId = 17, HigherId = 19 },
                new Rank { Id = 19, Name = "Генерал-Полковник", CounterToReach = 30, Color = "#00ffc0", LowerId = 18 }
            };
            modelBuilder.Entity<Rank>().HasData(ranks);

            /* 3. Подразделения */
            List<Subdivision> subdivisions = new List<Subdivision>
            {
                new Subdivision { Id = 1, Name = "Военная полиция", Description = "Следит за порядком и прилежным исполнением офицерами своих обязательств", Color = "#1721b8" },
                new Subdivision { Id = 2, Name = "Штаб", Description = "Координирует и повышает эффективность всех нижестоящих подразделений", Color = "#ad210c" },
                new Subdivision { Id = 3, Name = "Служба связи", Description = "Отвечает за Discord-сервер клана, за бота, сайт и АСБУ в целом", Color = "#7b8b00" },
                new Subdivision { Id = 4, Name = "1 Рота", Description = "1 Рота личного состава РХБЗ", Color = "#546e7a" },
                new Subdivision { Id = 5, Name = "Командование", AppendHeadName = true, Description = "Командование роты следит за поддержанием активности всех взводов", Color = "#a5553f", HeadId = 4 },
                new Subdivision { Id = 6, Name = "1 Пехотный взвод", AppendHeadName = true, Description = "Пехотный взвод личного состава РХБЗ", Color = "#95a5a6", HeadId = 4 },
                new Subdivision { Id = 7, Name = "2 Пехотный взвод", AppendHeadName = true, Description = "Пехотный взвод личного состава РХБЗ", Color = "#95a5a6", HeadId = 4 },
                new Subdivision { Id = 8, Name = "3 Механизированный взвод", AppendHeadName = true, Description = "Механизированный взвод личного состава РХБЗ", Color = "#95a5a6", HeadId = 4 },
                new Subdivision { Id = 9, Name = "4 Рекрутский взвод", AppendHeadName = true, Description = "Рекрутский взвод, состав которого проходит Курс Молодого Бойца", Color = "#95a5a6", HeadId = 4 },
            };
            modelBuilder.Entity<Subdivision>().HasData(subdivisions);

            /* 4. Должности */
            List<Post> posts = new List<Post>
            {
                new Post { Id = 1, Name = "Командир РХБЗ", Description = "Главнокомандующий клана", Color = "#f1c40f", MaxRankId = 19 },
                new Post { Id = 2, Name = "Заместитель командира РХБЗ", Description = "Заместитель главнокомандующий клана", Color = "#f1c40f", MaxRankId = 19, HeadId = 1 },
                new Post { Id = 3, Name = "Начальник военной полиции", Description = "Управляет военной полицией и исполняет должностные обязанности в высшем командовании", Color = "#003eeb", SubdivisionId = 1, MaxRankId = 19, HeadId = 2 },
                new Post { Id = 4, Name = "ОУп военной полиции", Description = "Особо уполномоченный военный полицейский, способный кикать и банить участников Discord сервера", Color = "#005ad3", SubdivisionId = 1, MaxRankId = 19, HeadId = 3 },
                new Post { Id = 5, Name = "Военный полицейский", Description = "Офицер военной полиции, следящий за порядком в клане", Color = "#0071c9", SubdivisionId = 1, MaxRankId = 19, HeadId = 4 },
                new Post { Id = 6, Name = "Начальник штаба", Description = "Управляет всеми нижестоящими подразделениями", Color = "#e92b0e", SubdivisionId = 2, MaxRankId = 19, HeadId = 2 },
                new Post { Id = 7, Name = "Заместитель начальника штаба", Description = "Управляет всеми нижестоящими подразделениями", Color = "#e92b0e", SubdivisionId = 2, MaxRankId = 19, HeadId = 6 },
                new Post { Id = 8, Name = "Командир", AppendSubdivisionName = true, Description = "Командир роты следит за исполнением взводных своих должностных обязанностей", Color = "#df6544", SubdivisionId = 5, MaxRankId = 19, HeadId = 7 },
                new Post { Id = 9, Name = "Замполит", AppendSubdivisionName = true, Description = "Заместитель командира роты следит за исполнением взводных своих должностных обязанностей", Color = "#c05e42", SubdivisionId = 5, MaxRankId = 19, HeadId = 8 },
                new Post { Id = 10, Name = "Командир", AppendSubdivisionName = true, Description = "Командир взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#9b59b6", SubdivisionId = 6, MaxRankId = 19, HeadId = 9 },
                new Post { Id = 11, Name = "Заместитель командира", AppendSubdivisionName = true, Description = "Заместитель командира взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#71368a", SubdivisionId = 6, MaxRankId = 19, HeadId = 10 },
                new Post { Id = 12, Name = "Командир", AppendSubdivisionName = true, Description = "Командир взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#9b59b6", SubdivisionId = 7, MaxRankId = 19, HeadId = 9 },
                new Post { Id = 13, Name = "Заместитель командира", AppendSubdivisionName = true, Description = "Заместитель командира взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#71368a", SubdivisionId = 7, MaxRankId = 19, HeadId = 12 },
                new Post { Id = 14, Name = "Командир", AppendSubdivisionName = true, Description = "Командир взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#9b59b6", SubdivisionId = 8, MaxRankId = 19, HeadId = 9 },
                new Post { Id = 15, Name = "Заместитель командира", AppendSubdivisionName = true, Description = "Заместитель командира взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#71368a", SubdivisionId = 8, MaxRankId = 19, HeadId = 14 },
                new Post { Id = 16, Name = "Командир", AppendSubdivisionName = true, Description = "Командир взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#9b59b6", SubdivisionId = 9, MaxRankId = 19, HeadId = 9 },
                new Post { Id = 17, Name = "Заместитель командира", AppendSubdivisionName = true, Description = "Заместитель командира взвода поддерживает активность своего взвода и повышает своих бойцов", Color = "#71368a", SubdivisionId = 9, MaxRankId = 19, HeadId = 16 },
                new Post { Id = 18, Name = "Начальник службы связи", Description = "Отвечает за всю техническую составляющую клана", Color = "#a2b800", SubdivisionId = 3, MaxRankId = 14, HeadId = 7 },
                new Post { Id = 19, Name = "Офицер службы связи", Description = "Отвечает за всю техническую составляющую клана", Color = "#8c9e00", SubdivisionId = 3, MaxRankId = 14, HeadId = 18 }
            };
            modelBuilder.Entity<Post>().HasData(posts);

            /* 5. Награды (Rewards) */
            List<Reward> rewards = new List<Reward>
            {
                new Reward { Id = 1, Name = "Орден Мужества", Conditions = "За героическое спасение отряда в хардкорной операции", Privileges = "Иммунитет к первому выговору", Color = "#ff4500" },
                new Reward { Id = 2, Name = "Почетный Связист", Conditions = "За безупречную отладку АСБУ клана без падений прода", Privileges = "Доступ в закрытую комнату разработчиков", Color = "#7b8b00" },
                new Reward { Id = 3, Name = "Почетный пекарь", Conditions = "За мужественное выпекание булочек с корицей", Privileges = "Безлимитный доступ в полевую кухню", Color = "#b923de" }
            };
            modelBuilder.Entity<Reward>().HasData(rewards);


            modelBuilder.Entity<FavoriteKit>().HasData(
                new FavoriteKit { Id = 1, Name = "Стрелок" },
                new FavoriteKit { Id = 2, Name = "Марксмен" },
                new FavoriteKit { Id = 3, Name = "Пилот" }
            );

            modelBuilder.Entity<BackgroundPicture>().HasData(
                new BackgroundPicture { Id = 1, Name = "Default Background" }
            );

            ulong myDiscordId = 412655474958270464;

            var units = new List<Unit>
            {
                new Unit
                {
                    DiscordId = myDiscordId,
                    Nickname = "Администратор (Я)",
                    SteamId = 76561198000000000,
                    RankUpCounter = 0,
                    FavoriteKitId = 1,
                    BackgroundPictureId = 1
                },

                new Unit
                {
                    DiscordId = 632641236412378,
                    Nickname = "Дениска",
                    SteamId = 632641236412378,
                    RankUpCounter = 0,
                    FavoriteKitId = 1,
                    BackgroundPictureId = 1
                },

                new Unit
                {
                    DiscordId = 345678901234567890,
                    Nickname = "NikitaNet",
                    SteamId = 76561198000000002,
                    RankUpCounter = 0,
                    FavoriteKitId = 2,
                    BackgroundPictureId = 1
                },

                new Unit
                {
                    DiscordId = 456789012345678901,
                    Nickname = "Ярек",
                    SteamId = 76561198000000003,
                    RankUpCounter = 0,
                    FavoriteKitId = 3,
                    BackgroundPictureId = 1
                }
            };
            modelBuilder.Entity<Unit>().HasData(units);

            modelBuilder.Entity<AssignedRank>().HasData(
                new AssignedRank { Id = 1, UnitId = myDiscordId, RankId = 19, Start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedRank { Id = 2, UnitId = 632641236412378, RankId = 17, Start = new DateTime(2024, 4, 12, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedRank { Id = 3, UnitId = 345678901234567890, RankId = 12, Start = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedRank { Id = 4, UnitId = 456789012345678901, RankId = 16, Start = new DateTime(2023, 11, 24, 0, 0, 0, DateTimeKind.Utc) }
            );

            modelBuilder.Entity<AssignedPost>().HasData(
                new AssignedPost { Id = 5, UnitId = myDiscordId, PostId = 1, Start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedPost { Id = 6, UnitId = 632641236412378, PostId = 6, Start = new DateTime(2024, 4, 12, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedPost { Id = 7, UnitId = 345678901234567890, PostId = 18, Start = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
                new AssignedPost { Id = 8, UnitId = 456789012345678901, PostId = 10, Start = new DateTime(2023, 11, 24, 0, 0, 0, DateTimeKind.Utc) }
            );

            modelBuilder.Entity<Activity>().HasData(
                new Activity { UnitId = myDiscordId, Date = DateOnly.FromDateTime(DateTime.Today) },
                new Activity { UnitId = 632641236412378, Date = DateOnly.FromDateTime(DateTime.Today) },
                new Activity { UnitId = 345678901234567890, Date = DateOnly.FromDateTime(DateTime.Today) },
                new Activity { UnitId = 456789012345678901, Date = DateOnly.FromDateTime(DateTime.Today) }
            );

            modelBuilder.Entity<GivedPermission<Post>>().HasData(
                new GivedPermission<Post>
                {
                    Id = 1,
                    PermissionType = PermissionType.Administrator,
                    EntityId = 1,
                    Inherit = true
                }
            );

            modelBuilder.Entity<GivedPermission<Rank>>().HasData(
                new GivedPermission<Rank>
                {
                    Id = 1,
                    PermissionType = PermissionType.Administrator,
                    EntityId = 5,
                    Inherit = true
                }
            );
        }
    }
}
