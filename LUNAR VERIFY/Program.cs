using Discord;
using Discord.WebSocket;
using DiscordBot;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleHttp;
using System.Net;
using System.Text;
using System.Web;
namespace LUNAR_VERIFY
{
    public class Program
    {
        private static FirestoreDb? db;
        private readonly DiscordSocketClient _client;
        private static ulong ServerId, AdminId;
        private static ulong SubRoleId;
        private static ulong UserRoleId;
        private static string GCPApiKey = string.Empty;
        private static string ClientId = string.Empty;
        private static string AdminChannel = string.Empty;
        private static string URL = string.Empty;
        private static string logmessage = string.Empty;
        private static string UserChannel = string.Empty;
        private static List<string> UserEmails = [];
        public Dictionary<string, string> ErrorMsg = new()
        {
            { "The requester is not allowed to access the requested subscriptions.", "채널이 구독 공개되어 있지 않습니다.\n" + @"https://www.youtube.com/account_privacy 에서 내 구독정보 모두 비공개 항목을 꺼주세요" },
            { "The subscriber identified with the request cannot be found.", "올바르지 않은 채널 아이디 입니다.\n/채널아이디 를 사용하세요" }
        };
        public static void Main()
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }
        private static void Responed(string message, HttpListenerResponse res)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(message);
            res.Headers["Content-Type"] = "text/html; charset=utf-8";
            res.OutputStream.Write(utf8Bytes, 0, utf8Bytes.Length);
            res.WithCORS().Close();
        }
        public async Task MainAsync()
        {
            await LoadInDBAsync();
            await _client.StartAsync();
            Route.Add("/", (req, res, props) =>
            {
                Responed("test", res);
            });
            string templatePath = Path.Combine(Environment.CurrentDirectory, "Templates");
            Route.Add("/callback", (req, res, props) =>
            {
                Responed($"<script>window.location.href = '{URL}/callback/' + window.location.hash.substring(1).split('=')[1].split('&')[0];</script>", res);
            });
            Route.Add("/callback2", (req, res, props) =>
            {
                Responed($"<script>window.location.href = '{URL}/callback2/' + window.location.hash.substring(1).split('=')[1].split('&')[0];</script>", res);
            });
            Route.Add("/callback2/{Access_Token}", async (req, res, props) =>
            {
                string accessToken = props["Access_Token"];
                string apiEndpoint = "https://people.googleapis.com/v1/people/me?personFields=emailAddresses";
                using (HttpClient httpClient = new())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {HttpUtility.UrlDecode(accessToken)}");
                    HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);
                    response.EnsureSuccessStatusCode();
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    JObject Json = JObject.Parse(jsonContent);
                    foreach (var item in Json["emailAddresses"]!)
                    {
                        UserEmails.Add((string)item["value"]!);
                    }
                }
                res.AsFile(req, Path.Combine(templatePath, "OAuth2Email.html"));
            });
            Route.Add("/callback/{Access_Token}", async (req, res, props) =>
            {
                string accessToken = props["Access_Token"];
                string apiEndpoint = "https://www.googleapis.com/youtube/v3/channels?part=contentDetails&mine=true";
                using (HttpClient httpClient = new())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);
                    response.EnsureSuccessStatusCode();
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    JObject jsonResponseObj = JObject.Parse(jsonContent);
                    JToken firstItem = jsonResponseObj["items"]!.FirstOrDefault()!;
                    string channelId = (string)firstItem?["id"]!;
                    UserChannel = channelId;
                }

                res.AsFile(req, Path.Combine(templatePath, "OAuth2Youtube.html"));
            });
            Route.Add("/VerifyUser", async (req, res, props) =>
            {
                string ChannelId = UserChannel;
                var emails = UserEmails;
                SocketUser User = _client.GetUser(req.Headers["UserName"]);
                if (User is not null)
                {
                    if (req.Headers["type"] == "youtube")
                    {
                        if (ChannelId != "ERROR")
                        {
                            SocketGuildUser GuildUser = _client.GetGuild(ServerId).GetUser(User.Id);
                            bool isAlreadyExist = false;
                            foreach (var item in await db!.Collection("Channel").ListDocumentsAsync().ToListAsync())
                            {
                                if ((await item.GetSnapshotAsync()).GetValue<string>("ChannelId") == ChannelId)
                                {
                                    isAlreadyExist = true;
                                }
                            }
                            if (!isAlreadyExist)
                            {
                                if (!GuildUser.Roles.Contains(_client.GetGuild(ServerId).GetRole(UserRoleId)))
                                {
                                    await GuildUser.AddRoleAsync(_client.GetGuild(ServerId).GetRole(UserRoleId));
                                    await db.Collection("Channel").Document(req.Headers["UserName"]).CreateAsync(
                                    new Dictionary<string, object>()
                                    {
                                    { "ChannelId", ChannelId }
                                    });
                                    EmbedBuilder AdminSuccessEmbed = new()
                                    {
                                        Title = "채널 링크 인증 성공 알림",
                                        Description = $"인증한 채널 : https://www.youtube.com/channel/{ChannelId}",
                                        Color = Color.Green,
                                        Footer = new EmbedFooterBuilder()
                                        {
                                            Text = GuildUser.Username,
                                            IconUrl = GuildUser.GetAvatarUrl()
                                        }
                                    };
                                    await _client.GetUser(AdminId).SendMessageAsync(embed: AdminSuccessEmbed.Build());
                                    res.WithCode(HttpStatusCode.OK);
                                    Responed(ChannelId, res);
                                    UserChannel = string.Empty;
                                }
                                else
                                {
                                    res.WithCode(HttpStatusCode.BadRequest);
                                    Responed("이미 인증이 되어있는 유저입니다.", res);
                                }
                            }
                            else
                            {
                                res.WithCode(HttpStatusCode.BadRequest);
                                Responed("이미 인증이 되어있는 채널입니다.", res);
                            }
                        }
                        else
                        {
                            res.WithCode(HttpStatusCode.BadRequest);
                            Responed("다른 구글 계정으로 인증해주세요.", res);
                        }
                    }
                    else if (req.Headers["type"] == "email")
                    {
                        SocketGuildUser GuildUser = _client.GetGuild(ServerId).GetUser(User.Id);
                        bool isAlreadyExist = false;
                        foreach (var item in await db!.Collection("Email").ListDocumentsAsync().ToListAsync())
                        {
                            isAlreadyExist = HasCommonItem((await item.GetSnapshotAsync()).GetValue<List<string>>("EmailAddress"), emails);
                        }
                        if (!isAlreadyExist)
                        {
                            if (!GuildUser.Roles.Contains(_client.GetGuild(ServerId).GetRole(UserRoleId)))
                            {
                                await GuildUser.AddRoleAsync(_client.GetGuild(ServerId).GetRole(UserRoleId));
                                await db.Collection("Email").Document(req.Headers["UserName"]).CreateAsync(
                                new Dictionary<string, object>()
                                {
                                    { "EmailAddress", emails }
                                });
                                EmbedBuilder AdminSuccessEmbed = new()
                                {
                                    Title = "이메일 인증 성공 알림",
                                    Description = $"사용자 이메일 : {string.Join(", ", emails)}",
                                    Color = Color.Green,
                                    Footer = new EmbedFooterBuilder()
                                    {
                                        Text = GuildUser.Username,
                                        IconUrl = GuildUser.GetAvatarUrl()
                                    }
                                };
                                await _client.GetUser(AdminId).SendMessageAsync(embed: AdminSuccessEmbed.Build());
                                res.WithCode(HttpStatusCode.OK);
                                Responed(string.Join(", ", emails), res);
                                UserEmails = [];

                            }
                            else
                            {
                                res.WithCode(HttpStatusCode.BadRequest);
                                Responed("이미 인증이 되어있는 유저입니다.", res);
                            }
                        }
                        else
                        {
                            res.WithCode(HttpStatusCode.BadRequest);
                            Responed("이미 등록된 이메일입니다.", res);
                        }
                    }
                }
                else
                {
                    res.WithCode(HttpStatusCode.BadRequest);
                    Responed("올바르지 않은 사용자명입니다.", res);
                }
            }, "POST");
            ListenWeb();
        }
        static bool HasCommonItem<T>(List<T> list1, List<T> list2)
        {
            foreach (var item in list1)
            {
                if (list2.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }
        private static void ListenWeb()
        {
            HttpServer.ListenAsync(6969, CancellationToken.None, Route.OnHttpRequestAsync).Wait();
            ListenWeb();
        }
        public Program()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All,
                UseInteractionSnowflakeDate = false
            };
            _client = new DiscordSocketClient(config);
            _client.Log += Log;
            _client.Ready += Ready;
            _client.MessageReceived += MessageReceivedAsync;
            _client.ButtonExecuted += ButtonExecutedAsync;
            _client.ModalSubmitted += ModalSubmittedAsync;
            _client.SlashCommandExecuted += SlashCommandExecutedAsync;
        }
        public async Task LoadInDBAsync()
        {
            var GoogleSecretJson = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "Firebase.json");
            var credential = GoogleCredential.FromJson(GoogleSecretJson).CreateScoped();
            var firestoreClientBuilder = new FirestoreClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            };
            var client = firestoreClientBuilder.Build();
            db = FirestoreDb.Create((string)JObject.Parse(GoogleSecretJson)["project_id"]!, client);
            DocumentSnapshot botConfigSnap = await db.Collection("Config").Document("Bot").GetSnapshotAsync();
            DocumentSnapshot serverConfigSnap = await db.Collection("Config").Document("Server").GetSnapshotAsync();
            DocumentSnapshot youtubeConfigSnap = await db.Collection("Config").Document("Youtube").GetSnapshotAsync();
            AdminId = ulong.Parse(serverConfigSnap.GetValue<string>("AdminId"));
            ServerId = ulong.Parse(serverConfigSnap.GetValue<string>("ServerId"));
            SubRoleId = ulong.Parse(youtubeConfigSnap.GetValue<string>("SubRoleId"));
            UserRoleId = ulong.Parse(youtubeConfigSnap.GetValue<string>("UserRoleId"));
            AdminChannel = youtubeConfigSnap.GetValue<string>("YoutubeChannelId");
            GCPApiKey = youtubeConfigSnap.GetValue<string>("GCPAPIKEY");
            ClientId = youtubeConfigSnap.GetValue<string>("ClientId");
            URL = youtubeConfigSnap.GetValue<string>("URL");
            await _client.LoginAsync(TokenType.Bot, botConfigSnap.GetValue<string>("TOKEN"));
        }
        private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
        {
            List<SocketSlashCommandDataOption> CommandArg = [.. command.Data.Options];
            if (_client.GetGuild(ServerId).GetUser(command.User.Id).GuildPermissions.Administrator)
            {
                if (command.Data.Name == "인증")
                {
                    EmbedBuilder embed = new()
                    {
                        Title = (string)CommandArg[1].Value,
                        Description = (string)CommandArg[2].Value,
                        Color = (uint)Convert.ToInt32((string)CommandArg[3].Value, 16)
                    };
                    if ((string)CommandArg[0].Value == "OAuth2유튜브")
                    {
                        ButtonBuilder button = new()
                        {
                            Style = ButtonStyle.Link,
                            Label = "인증하기",
                            Url = $"https://accounts.google.com/o/oauth2/auth?client_id={ClientId}&redirect_uri={URL}/callback&scope=https://www.googleapis.com/auth/youtube&response_type=token"
                        };
                        ComponentBuilder component = new();
                        component.WithButton(button);
                        await command.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build());
                        EmbedBuilder AdminEmbed = new()
                        {
                            Title = "생성 성공",
                            Description = "임베드가 생성되었습니다.",
                            Color = Color.Green
                        };
                        await command.RespondAsync(embed: AdminEmbed.Build(), ephemeral: true);
                    }
                    else if ((string)CommandArg[0].Value == "유튜브")
                    {
                        ButtonBuilder button = new()
                        {
                            Label = "인증하기",
                            Style = ButtonStyle.Primary,
                            CustomId = "인증"
                        };
                        ComponentBuilder component = new();
                        component.WithButton(button);
                        await command.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build());
                        EmbedBuilder AdminEmbed = new()
                        {
                            Title = "생성 성공",
                            Description = "임베드가 생성되었습니다.",
                            Color = Color.Green
                        };
                        await command.RespondAsync(embed: AdminEmbed.Build(), ephemeral: true);
                    }
                    else if ((string)CommandArg[0].Value == "유튜브구독")
                    {
                        ButtonBuilder button = new()
                        {
                            Label = "인증하기",
                            Style = ButtonStyle.Primary,
                            CustomId = "구독인증"
                        };
                        ComponentBuilder component = new();
                        component.WithButton(button);
                        await command.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build());
                        EmbedBuilder AdminEmbed = new()
                        {
                            Title = "생성 성공",
                            Description = "임베드가 생성되었습니다.",
                            Color = Color.Green
                        };
                        await command.RespondAsync(embed: AdminEmbed.Build(), ephemeral: true);
                    }
                    else if ((string)CommandArg[0].Value == "OAuth2이메일")
                    {
                        ButtonBuilder button = new()
                        {
                            Style = ButtonStyle.Link,
                            Label = "인증하기",
                            Url = $"https://accounts.google.com/o/oauth2/auth?client_id={ClientId}&redirect_uri={URL}/callback2&scope=https://www.googleapis.com/auth/userinfo.email%20profile&response_type=token"
                        };
                        ComponentBuilder component = new();
                        component.WithButton(button);
                        await command.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build());
                        EmbedBuilder AdminEmbed = new()
                        {
                            Title = "생성 성공",
                            Description = "임베드가 생성되었습니다.",
                            Color = Color.Green
                        };
                        await command.RespondAsync(embed: AdminEmbed.Build(), ephemeral: true);
                    }
                    else if ((string)CommandArg[0].Value == "버튼")
                    {
                        ButtonBuilder button = new()
                        {
                            Label = "인증하기",
                            Style = ButtonStyle.Primary,
                            CustomId = "버튼인증"
                        };
                        ComponentBuilder component = new();
                        component.WithButton(button);
                        await command.Channel.SendMessageAsync(embed: embed.Build(), components: component.Build());
                        EmbedBuilder AdminEmbed = new()
                        {
                            Title = "생성 성공",
                            Description = "임베드가 생성되었습니다.",
                            Color = Color.Green
                        };
                        await command.RespondAsync(embed: AdminEmbed.Build(), ephemeral: true);
                    }
                }
            }
        }
        public async Task ButtonExecutedAsync(SocketMessageComponent component)
        {
            switch (component.Data.CustomId)
            {
                case "구독인증":
                    {
                        var userchannel_snap = await db!.Collection("Channel").Document(component.User.Username).GetSnapshotAsync();
                        string ChannelId = userchannel_snap.GetValue<string>("ChannelId");
                        EmbedBuilder FailedEmbed = new()
                        {
                            Title = "구독인증 실패",
                            Color = Color.Red,
                            Footer = new EmbedFooterBuilder()
                            {
                                Text = component.User.Username,
                                IconUrl = component.User.GetAvatarUrl()
                            }
                        };
                        TimeSpan time = (DateTimeOffset.Now - _client.GetGuild(ServerId).GetUser(component.User.Id).JoinedAt)!.Value;
                        if (time.TotalHours >= 24)
                        {
                            if (!_client.GetGuild(ServerId).GetUser(component.User.Id).Roles.Contains(_client.GetGuild(ServerId).GetRole(SubRoleId)))
                            {
                                try
                                {
                                    bool result = await Youtube.FindSubscribe(ChannelId, AdminChannel, GCPApiKey);
                                    if (result)
                                    {
                                        DocumentReference DOC = db!.Collection("Youtube").Document(ChannelId);
                                        Dictionary<string, object> data1 = new()
                                        {
                                            {"ID", component.User.Id },
                                            { "mention", component.User.Mention },
                                        };
                                        await DOC.SetAsync(data1);
                                        EmbedBuilder UserEmbed = new()
                                        {
                                            Title = "구독인증 성공",
                                            Description = $"{component.User.Mention}님 구독인증 완료되었습니다.",
                                            Color = Color.Green,
                                            Footer = new EmbedFooterBuilder()
                                            {
                                                Text = component.User.Username,
                                                IconUrl = component.User.GetAvatarUrl()
                                            }
                                        };
                                        await component.RespondAsync(embed: UserEmbed.Build(), ephemeral: true);
                                        EmbedBuilder AdminEmbed = new()
                                        {
                                            Title = "구독인증 성공 알림",
                                            Description = $"{component.User.Username}님이 구독인증에 성공했습니다.",
                                            Color = Color.Purple,
                                            Footer = new EmbedFooterBuilder()
                                            {
                                                Text = component.User.Username,
                                                IconUrl = component.User.GetAvatarUrl()
                                            }
                                        };
                                        await _client.GetUser(AdminId).SendMessageAsync(embed: AdminEmbed.Build());
                                        await _client.GetGuild(ServerId).GetUser(component.User.Id).AddRoleAsync(SubRoleId);
                                    }
                                    else
                                    {
                                        FailedEmbed.Description = "구독이 되어있지 않은 채널입니다.";
                                        await component.RespondAsync(embed: FailedEmbed.Build(), ephemeral: true);

                                        await _client.GetUser(AdminId).SendMessageAsync(embed: FailedEmbed.Build());
                                    }
                                }
                                catch (Exception ex)
                                {
                                    FailedEmbed.Description = ErrorMsg.TryGetValue(ex.Message, out string? errormesage) ? errormesage : ex.Message;
                                    await component.RespondAsync(embed: FailedEmbed.Build(), ephemeral: true);
                                    await _client.GetUser(AdminId).SendMessageAsync(embed: FailedEmbed.Build());
                                }
                            }
                            else
                            {
                                FailedEmbed.Description = "이미 구독인증이 되어있는 유저입니다.";
                                await component.RespondAsync(embed: FailedEmbed.Build(), ephemeral: true);
                                await _client.GetUser(AdminId).SendMessageAsync(embed: FailedEmbed.Build());
                            }
                        }
                        else
                        {
                            FailedEmbed.Description = "서버에 들어온 지 24시간이 지나야 구독인증이 가능합니다.\n!JoinedAt (유저 멘션)명령어로 서버에 들어온 날짜를 확인하세요.";
                            await component.RespondAsync(embed: FailedEmbed.Build(), ephemeral: true);
                            await _client.GetUser(AdminId).SendMessageAsync(embed: FailedEmbed.Build());
                        }
                        break;
                    }
                case "인증":
                    {
                        ModalBuilder modal = new()
                        {
                            Title = "유튜브 채널 인증",
                            CustomId = "채널인증"
                        };
                        TextInputBuilder text = new()
                        {
                            Label = "채널 링크",
                            CustomId = "채널링크",
                            Placeholder = "본인의 유튜브 채널을 입력해주세요."
                        };
                        modal.AddTextInput(text);
                        await component.RespondWithModalAsync(modal.Build());
                        break;
                    }
                case "버튼인증":
                    {
                        await _client.GetGuild(ServerId).GetUser(component.User.Id).AddRoleAsync(_client.GetGuild(ServerId).GetRole(UserRoleId));
                        EmbedBuilder SuccessEmbed = new()
                        {
                            Title = "인증 성공",
                            Description = "역할이 지급되었습니다.",
                            Color = Color.Green,
                            Footer = new EmbedFooterBuilder()
                            {
                                Text = component.User.Username,
                                IconUrl = component.User.GetAvatarUrl()
                            }
                        };
                        await component.RespondAsync(embed: SuccessEmbed.Build(), ephemeral: true);
                        break;
                    }
            }
        }
        public async Task ModalSubmittedAsync(SocketModal modal)
        {
            List<SocketMessageComponentData> components = [.. modal.Data.Components];
            switch (modal.Data.CustomId)
            {
                case "채널인증":
                    {
                        ChannelVerify(modal, components);
                        await Task.Delay(1);
                        break;
                    }
            }
        }
        public async void ChannelVerify(SocketModal modal, List<SocketMessageComponentData> components)
        {
            string ChannelId;
            try
            {
                ChannelId = await Youtube.GetChannelIdAsync(components[0].Value);
            }
            catch (Exception ex)
            {
                EmbedBuilder ChannelIdFailedEmbed = new()
                {
                    Title = "채널 아이디 확인 실패",
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder()
                    {
                        Text = modal.User.Username,
                        IconUrl = modal.User.GetAvatarUrl()
                    },
                    Description = ErrorMsg.TryGetValue(ex.Message, out string? descrip) ? $"채널 아이디 확인 실패 : {descrip}" : $"채널 아이디 확인 실패 : {ex.Message}"
                };
                await modal.RespondAsync(embed: ChannelIdFailedEmbed.Build(), ephemeral: true);
                await _client.GetUser(AdminId).SendMessageAsync(embed: ChannelIdFailedEmbed.Build());
                return;
            }
            bool isAlreadyExist = false;
            foreach (var item in await db!.Collection("Channel").ListDocumentsAsync().ToListAsync())
            {
                if ((await item.GetSnapshotAsync()).GetValue<string>("ChannelId") == ChannelId)
                {
                    isAlreadyExist = true;
                }
            }
            if (!isAlreadyExist)
            {
                EmbedBuilder SuccessEmbed = new()
                {
                    Title = "채널 링크 인증 성공",
                    Description = $"채널 아이디 확인 성공 : {ChannelId}\n3초 뒤 역할이 지급됩니다.",
                    Color = Color.Green,
                    Footer = new EmbedFooterBuilder()
                    {
                        Text = modal.User.Username,
                        IconUrl = modal.User.GetAvatarUrl()
                    }
                };
                await modal.RespondAsync(embed: SuccessEmbed.Build(), ephemeral: true);
                await Task.Delay(3000);
                await _client.GetGuild(ServerId).GetUser(modal.User.Id).AddRoleAsync(_client.GetGuild(ServerId).GetRole(UserRoleId));
                Dictionary<string, object> data1 = new()
                {
                    { "ChannelId", ChannelId }
                };
                await db.Collection("Channel").Document(modal.User.Username).CreateAsync(data1);
                EmbedBuilder AdminSuccessEmbed = new()
                {
                    Title = "채널 링크 인증 성공 알림",
                    Description = $"인증한 채널 : https://www.youtube.com/channel/{ChannelId}",
                    Color = Color.Green,
                    Footer = new EmbedFooterBuilder()
                    {
                        Text = modal.User.Username,
                        IconUrl = modal.User.GetAvatarUrl()
                    }
                };
                await _client.GetUser(AdminId).SendMessageAsync(embed: AdminSuccessEmbed.Build());
            }
            else
            {
                EmbedBuilder FailedEmbed = new()
                {
                    Title = "채널 링크 인증 실패",
                    Description = "이미 인증이 되어있는 채널입니다.",
                    Color = Color.Red,
                    Footer = new EmbedFooterBuilder()
                    {
                        Text = modal.User.Username,
                        IconUrl = modal.User.GetAvatarUrl()
                    }
                };
                await modal.RespondAsync(embed: FailedEmbed.Build(), ephemeral: true);
            }
        }
        public static void ConsoleLog(string message)
        {
            logmessage += message + "\n";
            Console.WriteLine(message);
        }
        private async Task Ready()
        {
            ApplicationCommandOptionChoiceProperties[] VerifyType =
            [
                new()
                {
                    Name = "OAuth2(유튜브)",
                    Value = "OAuth2유튜브",
                },
                new()
                {
                    Name = "유튜브 채널",
                    Value = "유튜브",
                },
                new()
                {
                    Name = "유튜브 구독인증",
                    Value = "유튜브구독",
                },
                new()
                {
                    Name = "OAuth2(이메일)",
                    Value = "OAuth2이메일",
                },
                new()
                {
                    Name = "버튼",
                    Value = "버튼",
                },

            ];
            var Verify = new SlashCommandBuilder()
            .WithName("인증")
            .WithDescription("사용자에게 보여줄 인증메세지를 출력합니다.")
            .AddOption("타입", ApplicationCommandOptionType.String, "사용자가 인증할 방법입니다.", isRequired: true, choices: VerifyType)
            .AddOption("제목", ApplicationCommandOptionType.String, "출력될 임베드의 제목입니다.", isRequired: true)
            .AddOption("설명", ApplicationCommandOptionType.String, "출력될 임베드의 설명입니다.", isRequired: true)
            .AddOption("색상", ApplicationCommandOptionType.String, "출력될 임베드의 색상입니다. (HTML 컬러코드를 참고하세요)", isRequired: true);
            await _client.CreateGlobalApplicationCommandAsync(Verify.Build());
            ConsoleLog($"{DateTime.Now:HH:mm:ss} Gateway     Connected Bot : {_client.CurrentUser}");
        }

        private Task Log(LogMessage log)
        {
            ConsoleLog(log.ToString());
            return Task.CompletedTask;
        }
        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Content.StartsWith("!JoinedAt "))
            {
                ulong userid = ulong.Parse(message.Content.Replace("!JoinedAt <@", "").Replace(">", ""));
                TimeSpan time = (DateTimeOffset.Now - _client.GetGuild(ServerId).GetUser(userid).JoinedAt)!.Value;
                await message.Channel.SendMessageAsync($"{time.Days}일 {time.Hours}시간 {time.Minutes}분");
            }
        }
    }
}
