using Business.Models.Abstraction;
using Business.Models.Enums;
using Business.Models.Interfaces;
using Business.Models.Statuses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Models
{
	public class Rank : IEntityWithDiscordRole, IEntityWithFiles
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public ushort CounterToReach { get; set; }
		public string Color { get; set; } = "#00FF00";
		public ulong? DiscordRoleId { get; set; }
		public int Index { get; set; }
		[JsonIgnore] public virtual HashSet<GivedPermission<Rank>> GivedPermissions { get; set; } = new HashSet<GivedPermission<Rank>>();
		[JsonIgnore] public virtual List<AssignedRank> AssignedRanks { get; set; } = new List<AssignedRank>();

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }

        public HashSet<Permission> GetPermissions()
        {
            return GivedPermissions.Select(gp => gp.Permission).ToHashSet();
        }

		public void UpdateRole()
		{
			if (DiscordRoleId != null)
			{
				// TODO: Send request to discord-bot api
			}
		}

		public void CheckRoleOnUser(ulong unitId)
		{
			if (DiscordRoleId != null)
			{
				// TODO: Send request to discord-bot api
			}
		}

        public string GetFilesFolderName()
        {
			return "ranks";
        }
    }

	public class RankConfiguration : IEntityTypeConfiguration<Rank>
	{
		public void Configure(EntityTypeBuilder<Rank> builder)
		{
			builder.HasIndex(r => r.Index).IsUnique();
			builder.HasMany(r => r.GivedPermissions).WithOne(gp => gp.Entity).HasForeignKey(gp => gp.EntityId).OnDelete(DeleteBehavior.Cascade);
			builder.HasMany(r => r.AssignedRanks).WithOne(ar => ar.Rank).HasForeignKey(r => r.RankId).OnDelete(DeleteBehavior.Cascade);
		}
	}
}
