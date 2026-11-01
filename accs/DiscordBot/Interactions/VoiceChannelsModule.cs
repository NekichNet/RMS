using accs.Database;
using accs.DiscordBot.Interactions.Enums;
using accs.Models;
using accs.Models.Enums;
using accs.Services.Interfaces;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace accs.DiscordBot.Interactions
{
    [Group("voice", "Управление голосовыми каналами")]
    public class VoiceChannelsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<VoiceChannelsModule> _log;
		private readonly DiscordSocketClient _client;
        private readonly IGuildProviderService _guildProvider;
        private readonly AppDbContext _db;

		private ulong _voiceChannelId;

        public VoiceChannelsModule(ILogger<VoiceChannelsModule> log, DiscordSocketClient discordSocketClient, IGuildProviderService guildProvider, AppDbContext db) 
        {
            _log = log;
            _client = discordSocketClient;
            _guildProvider = guildProvider;
            _db = db;
		}

        public override void OnModuleBuilding(InteractionService commandService, ModuleInfo module)
        {
            base.OnModuleBuilding(commandService, module);

			string voiceChannelIdString = DotNetEnv.Env.GetString("VOICE_CHANNEL_ID", "null");
			if (!ulong.TryParse(voiceChannelIdString, out _voiceChannelId)) { _log.LogError("Cannot parse voice channel id!"); }

			_client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
		}

        public async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
			SocketGuild guild = _guildProvider.GetGuild();

			if (before.VoiceChannel is SocketVoiceChannel)
            {
				if (before.VoiceChannel.ConnectedUsers.Count == 0 && before.VoiceChannel.Id != _voiceChannelId && !(before.VoiceChannel.Name.StartsWith("【🔉】") || before.VoiceChannel.Name.StartsWith("【🎙️】")))
				{
					await before.VoiceChannel.DeleteAsync();
				}
			}

            if (after.VoiceChannel == null)
                return;

			if (after.VoiceChannel.Id == _voiceChannelId)
            {
                const string channelDefaultName = "Голосовой";
                var channels = guild.Channels;
                List<string> channelNames = new List<string>();

                foreach (var channel in channels) {
                    if (channel.ChannelType == ChannelType.Voice) 
                    {
                        if (channel.Name.Contains($"【🔊】{channelDefaultName}"))
                        {
                            channelNames.Add(channel.Name);
                        }
                    }
                }

                int freeNumber = 1;
                while (channelNames.Contains($"【🔊】{channelDefaultName} {freeNumber}"))
                {
                    freeNumber++;
                }

                ulong voiceCategoryId;
                if (!ulong.TryParse(DotNetEnv.Env.GetString("VOICE_CATEGORY_ID", "VOICE_CATEGORY_ID not found"), out voiceCategoryId))
                {
                    _log.LogError("Cannot parse voice category id!");
                    return;
                }

                /// Channel creation and block for everyone to connect
                var newChannel = await guild.CreateVoiceChannelAsync($"【🔊】{channelDefaultName} {freeNumber}", (props) => { props.Bitrate = 64000; props.UserLimit = null; props.CategoryId = voiceCategoryId; props.RTCRegion = after.VoiceChannel.RTCRegion; });

                /// Permission for needed users being granted
                SocketGuildUser guildUser = guild.GetUser(user.Id);
                // await newChannel.AddPermissionOverwriteAsync(guildUser, new OverwritePermissions(manageChannel: PermValue.Allow)); согласно приказу, теперь только определённые должности имеют право переименовывать
                await guild.MoveAsync(guildUser, newChannel);
            }

		}

        //[SlashCommand("access", "Откройте доступ к каналу для клана или для всех")] 
        public async Task OnGivingAccessCommand(AccessChoices accessChoices)
        {
			SocketGuild guild = _guildProvider.GetGuild();

			if (accessChoices == AccessChoices.Friend)
            {
                await guild.GetChannel(Context.Interaction.Channel.Id).AddPermissionOverwriteAsync(guild.GetRole(0), new OverwritePermissions(connect: PermValue.Allow));
            }
            else if (accessChoices == AccessChoices.Clan)
            {
                await guild.GetChannel(Context.Interaction.Channel.Id).AddPermissionOverwriteAsync(guild.GetRole(0), new OverwritePermissions(connect: PermValue.Allow));
            }
        }


        //[SlashCommand("access-role", "Откройте доступ к каналу для определённой роли")]
        public async Task OnGivingAccessByRoleCommand(SocketRole role)
        {
			SocketGuild guild = _guildProvider.GetGuild();
			await guild.GetChannel(Context.Interaction.Channel.Id).AddPermissionOverwriteAsync(role, new OverwritePermissions(connect: PermValue.Allow));
        }
    }
}
