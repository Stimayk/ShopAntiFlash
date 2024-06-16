using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopAntiFlash
{
    public class ShopAntiFlash : BasePlugin
    {
        public override string ModuleName => "[SHOP] Anti Flash";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.0";

        private IShopApi? SHOP_API;
        private const string CategoryName = "AntiFlash";
        public static JObject? JsonAntiFlash { get; private set; }
        private readonly PlayerAntiFlash[] playerAntiFlash = new PlayerAntiFlash[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/AntiFlash.json");
            if (File.Exists(configPath))
            {
                JsonAntiFlash = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonAntiFlash == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Anti Flash");

            var sortedItems = JsonAntiFlash
                .Properties()
                .Select(p => new { Key = p.Name, Value = (JObject)p.Value })
                .OrderBy(p => (int)p.Value["level"]!)
                .ToList();

            foreach (var item in sortedItems)
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(item.Key, (string)item.Value["name"]!, CategoryName, (int)item.Value["price"]!, (int)item.Value["sellprice"]!, (int)item.Value["duration"]!);
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerAntiFlash[playerSlot] = null!);

            RegisterEventHandler<EventPlayerBlind>((@event, info) =>
            {
                var player = @event.Userid;

                if (player == null || !player.IsValid || playerAntiFlash[player.Slot] == null) return HookResult.Continue;

                var featureValue = playerAntiFlash[player.Slot].Level;
                var attacker = @event.Attacker;

                var playerPawn = player.PlayerPawn.Value;
                if (playerPawn == null || playerPawn.LifeState is not (byte)LifeState_t.LIFE_ALIVE) return HookResult.Continue;

                var sameTeam = attacker?.Team == player.Team;

                switch (featureValue)
                {
                    case 1:
                        if (sameTeam == true && player != attacker)
                            playerPawn.FlashDuration = 0.0f;
                        break;
                    case 2:
                        if (player == attacker)
                            playerPawn.FlashDuration = 0.0f;
                        break;
                    case 3:
                        if (sameTeam == true || player == attacker)
                            playerPawn.FlashDuration = 0.0f;
                        break;
                    default:
                        playerPawn.FlashDuration = 0.0f;
                        break;
                }

                return HookResult.Continue;
            });
        }

        public void OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName, int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetAntiFlashLevel(uniqueName, out int level))
            {
                playerAntiFlash[player.Slot] = new PlayerAntiFlash(level, itemId);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'level' in config!");
            }
        }

        public void OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetAntiFlashLevel(uniqueName, out int level))
            {
                playerAntiFlash[player.Slot] = new PlayerAntiFlash(level, itemId);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
        }

        public void OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerAntiFlash[player.Slot] = null!;
        }

        private static bool TryGetAntiFlashLevel(string uniqueName, out int level)
        {
            level = 0;
            if (JsonAntiFlash != null && JsonAntiFlash.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["level"] != null && jsonItem["level"]!.Type != JTokenType.Null)
            {
                level = (int)jsonItem["level"]!;
                return true;
            }
            return false;
        }

        public record class PlayerAntiFlash(int Level, int ItemID);
    }
}