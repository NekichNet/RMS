using Business.Database;
using Business.Models;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Business.Services
{
    public class StructureService : BusinessService
    {
		private readonly ILogger<StructureService> _logger;
		private readonly AppDbContext _db;

        public StructureService(AppDbContext db, ILogger<StructureService> logger)
        {
            _db = db;
			_logger = logger;
		}

        public async Task<ActionResult<List<object>>> GetStructureAsync()
        {
            ActionResult<List<object>> action = new ActionResult<List<object>>(_logger);

            try
            {
                var posts = await _db.Posts.ToListAsync();
                var subdivisions = await _db.Subdivisions.ToListAsync();
                var subdivisionsDict = subdivisions.ToDictionary(s => s.Id);

                List<Unit> units = (await _db.Units
                    .ToListAsync())
                    .Where(u => u.IsActive()).ToList();

                var result = posts.Select(post =>
                {
                    subdivisionsDict.TryGetValue(post.SubdivisionId ?? 0, out var sub);

                    var assignedUnits = units
                        .Where(u => u.GetPosts().Any(p => p.Id == post.Id))
                        .Select(u =>
                        {
                            var currentRank = u.GetRank();
                            return new
                            {
                                DiscordId = u.DiscordId.ToString(),
                                Nickname = u.Nickname,
                                SteamId = u.SteamId?.ToString(),
                                RankUpCounter = u.RankUpCounter,
                                FavoriteKitId = u.FavoriteKitId,
                                BackgroundPictureId = u.BackgroundPictureId,
                                Color = currentRank?.Color ?? "#FFFFFF",
                                Rank = currentRank != null ? new
                                {
                                    Name = currentRank.Name,
                                    Color = currentRank.Color ?? "#FFFFFF"
                                } : null
                            };
                        })
                        .ToList();

                    return new
                    {
                        Id = post.Id.ToString(),
                        Name = post.GetFullName(),
                        Description = post.Description,
                        Color = post.Color ?? "#FFFFFF",
                        HeadId = post.HeadId,
                        SubdivisionId = post.SubdivisionId,
                        AppendSubdivisionName = post.AppendSubdivisionName,
                        Subdivision = sub != null ? new
                        {
                            Id = sub.Id,
                            Name = sub.Name,
                            Color = sub.Color ?? "#4b5563"
                        } : null,
                        Units = assignedUnits
                    };
                }).ToList<object>();

                action.Value = result;
                action.FormSuccess("Structure formed");
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

        private Dictionary<string, object> BuildPostStructure(Post post, List<Post> allPosts, List<Unit> allUnits)
        {
            var people = allUnits
                .Where(u => u.GetPosts().Any(p => p.Id == post.Id))
                .Select(u => u.Nickname)
                .ToList();

            var subordinates = allPosts
                .Where(p => p.HeadId == post.Id)
                .ToDictionary(
                    p => p.Name,
                    p => BuildPostStructure(p, allPosts, allUnits)
                );

            return new Dictionary<string, object>
            {
                { "Id", post.Id },
                { "Color", post.Color ?? "#FFFFFF" },
                { "People", people },
                { "Subdivision", post.SubdivisionId },
                { "Subordinates", subordinates }
            };
        }
    }
}
