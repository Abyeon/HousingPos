using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;
using HousingPos.Objects;
using System.Runtime.InteropServices;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Game.Command;
using HousingPos.Gui;
using Dalamud.Game.ClientState.Conditions;
using HousingPos.Utils;

namespace HousingPos
{
    public class HousingPos : IDalamudPlugin
    {
        public static string Name => "HousingPos";
        private PluginUi Gui { get; set; }
        public Configuration Config { get; private set; }

        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
        [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] private static IChatGui ChatGui { get; set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IDataManager Data { get; private set; } = null!;
        [PluginService] private static ISigScanner Scanner { get; set; } = null!;
        [PluginService] private static IGameInteropProvider Hook { get; set; } = null!;
        [PluginService] private static IPluginLog PluginLog { get; set; } = null!;
        [PluginService] public static ITextureProvider Tex { get; private set; } = null!;
        [PluginService] private static ICondition Condition { get; set; } = null!;

        private readonly Localizer _localizer;

        // Texture dictionary for the housing item icons.
        // public readonly Dictionary<ushort, TextureWrap> TextureDictionary = new Dictionary<ushort, TextureWrap>();
        
        public List<HousingItem> HousingItemList = [];
        public string HouseSize = "";
        public string HouseName = "";
        
        private readonly List<int> _previewPages = [];
        
        public int PreviewTerritory;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate Int64 LoadHousingFuncDelegate(long a1, long a2);
        private readonly Hook<LoadHousingFuncDelegate> _loadHousingFuncHook;

        public HousingPos()
        {
            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
            RefreshFurnitureList(ref Config.HousingItemList);
            Config.Grouping = false;
            Config.Save();
            _localizer = new Localizer(Config.UILanguage);
            
            var loadHousingFunc = Scanner.ScanText("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 48 8B 71 08 48 8B FA");
            Condition.ConditionChange += OnConditionChange;
            
            _loadHousingFuncHook = Hook.HookFromAddress<LoadHousingFuncDelegate>(
                loadHousingFunc,
                LoadHousingFuncDetour
            );
            
            _loadHousingFuncHook.Enable();

            CommandManager.AddHandler("/xhouse", new CommandInfo(CommandHandler)
            {
                HelpMessage = "/xhouse - load housing item list."
            });
            Gui = new PluginUi(this);
            ClientState.TerritoryChanged += TerritoryChanged;
        }
        
        public void Dispose()
        {
            Condition.ConditionChange -= OnConditionChange;
            _loadHousingFuncHook.Disable();
            _loadHousingFuncHook.Dispose();
            
            ClientState.TerritoryChanged -= TerritoryChanged;
            CommandManager.RemoveHandler("/xhouse");
            Gui.Dispose();
            GC.SuppressFinalize(this);
        }


        private static void RefreshFurnitureList(ref List<HousingItem> furnitureList)
        {
            foreach (var housingItem in furnitureList.Where(t => t is { ModelKey: > 0, FurnitureKey: 0 }))
            {
                housingItem.FurnitureKey = (uint)(housingItem.ModelKey + 0x30000);
                HousingFurniture? furniture = Data.GetExcelSheet<HousingFurniture>().GetRow(housingItem.FurnitureKey);
                housingItem.ModelKey = furniture.Value.ModelKey;
            }
        }

        public static void TranslateFurnitureList(ref List<HousingItem> furnitureList)
        {
            for (var i = 0; i < furnitureList.Count; i++)
            {
                if (furnitureList[i].FurnitureKey != 0) continue;
                
                RefreshFurnitureList(ref furnitureList);
                break;
            }
            
            foreach (var housingItem in furnitureList)
            {
                HousingFurniture? furniture = Data.GetExcelSheet<HousingFurniture>().GetRow(housingItem.FurnitureKey);
                housingItem.Name = furniture.Value.Item.Value.Name.ToString();
            }
            
            furnitureList = furnitureList.Where(e => e.Name != "").ToList();
        }


        private void OnConditionChange(ConditionFlag flag, bool value)
        {
            if (flag != ConditionFlag.UsingHousingFunctions) return;
            
            if (Config.Previewing)  // disable decorate UI
            {
                LogError(_localizer.Localize("Decorate in preview mode is dangerous!"));
                LogError(_localizer.Localize("Please exit the house and disable preview!"));
                return;
            }

            if (HousingItemList.Count > 0 && Config.HousingItemList.Count == 0)
            {
                Log(string.Format(_localizer.Localize("Load {0} furnitures."), HousingItemList.Count));
                
                RefreshFurnitureList(ref HousingItemList);
                RefreshFurnitureList(ref Config.HousingItemList);
                
                Config.HousingItemList = HousingItemList.ToList();
                Config.HiddenScreenItemHistory = [];

                Config.Save();
            }
            else
            {
                Log(_localizer.Localize("Please clear the furniture list and re-enter house to load current furniture list."));
            }
        }
        
        private long LoadHousingFuncDetour(long a1, long a2)
        {
            var dataPtr = (IntPtr)a2;
            //Log($"Housing data: {a2:x8}");

            byte[] posArr = new byte[2416];
            Marshal.Copy(dataPtr, posArr, 0, 12);
            if (BitConverter.ToString(posArr).Replace("-", " ").StartsWith("FF FF FF FF FF FF FF FF"))
            {
                HousingItemList.Clear();
                Config.DrawScreen = false;
                Config.Save();
                return this._loadHousingFuncHook.Original(a1, a2);
            }
            if (Config.Previewing)
            {
                RefreshFurnitureList(ref Config.HousingItemList);
                if (DateTime.Now > Config.lastPosPackageTime.AddSeconds(5))
                {
                    _previewPages.Clear();
                    Config.lastPosPackageTime = DateTime.Now;
                    Config.Save();
                }
                int curPage = 0;
                int count = 0;
                while (_previewPages.IndexOf(curPage) != -1)
                    curPage++;
                
                _previewPages.Add(curPage);
                List<string> compatibleTalks = new()
                {
                    "CmnDefHousingObject",
                    "CmnDefRetainerBell",
                    "ComDefCompanyChest",
                    "CmnDefBeautySalon",
                    "CmnDefCutSceneReplay",
                    "CmnDefMiniGame",
                    "CmnDefCabinet",
                    "HouFurVisitNote"
                };
                for (int i = 12; i < posArr.Length && i + 24 < posArr.Length; i += 24)
                {
                    var hashIndex = ((i - 12) / 24) + curPage * 100;
                    if (hashIndex >= Config.HousingItemList.Count) continue;
                    
                    count++;
                    var item = Config.HousingItemList[hashIndex];
                    var furniture = Data.GetExcelSheet<HousingFurniture>().GetRow(item.FurnitureKey);
                    ushort furnitureNetId = (ushort)(item.FurnitureKey - 0x30000);
                    byte[] itemBytes = new byte[24];
                    itemBytes[2] = 1;
                    if (furniture.CustomTalk.RowId > 0)
                    {
                        string talk = furniture.CustomTalk.Value.Name.ToString().Split('_')[0];
                        if (compatibleTalks.Contains(talk))
                        {
                            itemBytes[2] = 1;
                        }
                        else
                        {
                            switch (talk)
                            {
                                case "CmnDefHousingDish":
                                    itemBytes[2] = 0;
                                    break;
                                case "HouFurOrchestrion":
                                    itemBytes[2] = 2;
                                    break;
                                case "HouFurAquarium":
                                    furnitureNetId = 0x1EF;
                                    itemBytes[2] = 1;
                                    break;
                                case "HouFurVase":
                                    ushort oldId = furnitureNetId;
                                    furnitureNetId = oldId switch
                                    {
                                        196828 - 0x30000 => 196751 - 0x30000,
                                        196829 - 0x30000 => 196752 - 0x30000,
                                        _ => 196753 - 0x30000,
                                    };
                                    itemBytes[2] = 1;
                                    break;
                                case "HouFurPlantPot":
                                    furnitureNetId = 0x160;
                                    itemBytes[2] = 1;
                                    break;
                                case "HouFurPicture":
                                case "HouFurFishprint":
                                    furnitureNetId = (ushort)(furnitureNetId == 0x222 ? 0x2F0 : 0x1E);
                                    itemBytes[2] = 1;
                                    break;
                                case "HouFurWallpaperPartition":
                                    furnitureNetId = 0x20C;
                                    itemBytes[2] = 1;
                                    break;
                                default:
                                    PluginLog.Info($"ignore {furniture.Item.Value.Name}:{furniture.CustomTalk.Value.Name}");
                                    Array.Copy(itemBytes, 0, posArr, i, 24);
                                    count--;
                                    continue;
                            }
                        }
                    }

                    BitConverter.GetBytes(furnitureNetId).CopyTo(itemBytes, 0);
                    itemBytes[4] = item.Stain;
                    BitConverter.GetBytes(item.Rotate).CopyTo(itemBytes, 8);
                    BitConverter.GetBytes(item.X).CopyTo(itemBytes, 12);
                    BitConverter.GetBytes(item.Y).CopyTo(itemBytes, 16);
                    BitConverter.GetBytes(item.Z).CopyTo(itemBytes, 20);
                        
                    Array.Copy(itemBytes, 0, posArr, i, 24);
                }
                Log(String.Format(_localizer.Localize("Previewing {0} furnitures."), count));
                PreviewTerritory = ClientState.TerritoryType;
                Marshal.Copy(posArr, 0, dataPtr, 2416);
                return this._loadHousingFuncHook.Original(a1, a2);
            }


            Marshal.Copy(dataPtr, posArr, 0, 2416);
            if (DateTime.Now > Config.lastPosPackageTime.AddSeconds(5))
            {
                HousingItemList.Clear();
                Config.lastPosPackageTime = DateTime.Now;
                Config.Save();
            }

            HouseSize = TerritoryTools.GetHouseSize();
            HouseName = TerritoryTools.GetMakePlaceRenovation();
            
            for (int i = 12; i < posArr.Length && i + 24 < posArr.Length; i += 24)
            {
                uint furnitureKey = (uint)(BitConverter.ToUInt16(posArr, i) + 0x30000);
                var furniture = Data.GetExcelSheet<HousingFurniture>().GetRow(furnitureKey);
                var item = furniture.Item.Value;
                if (item.RowId == 0) continue;
#if DEBUG
                byte[] tmpArr = new byte[24];
                Array.Copy(posArr, i, tmpArr, 0, 24);
                PluginLog.Info($"{item.Name}:" + (BitConverter.ToString(tmpArr).Replace("-", " ")));
                if (furniture.CustomTalk.RowId > 0 || furniture.Item.Value.Name.ToString().EndsWith("空白隔离墙"))
                {
                    string talk = furniture.CustomTalk.Value.Name.ToString();
                    PluginLog.Info($"FurnitureTalk {furniture.Item.Value.Name}: {talk}");
                    PluginLog.Info(BitConverter.ToString(tmpArr).Replace("-", " "));
                }
#endif

                byte stain = posArr[i + 4];
                var rotate = BitConverter.ToSingle(posArr, i + 8);
                var x = BitConverter.ToSingle(posArr, i + 12);
                var y = BitConverter.ToSingle(posArr, i + 16);
                var z = BitConverter.ToSingle(posArr, i + 20);
                HousingItemList.Add(new HousingItem(
                        furnitureKey,
                        furniture.ModelKey,
                        item.RowId,
                        stain,
                        x,
                        y,
                        z,
                        rotate,
                        item.Name.ToString()
                    ));
            }
            return this._loadHousingFuncHook.Original(a1, a2);
        }
        private void TerritoryChanged(ushort e)
        {
            Config.DrawScreen = false;
            Config.Save();
        }

        private void CommandHandler(string command, string arguments)
        {
            var args = arguments.Trim().Replace("\"", string.Empty);

            if (!string.IsNullOrEmpty(args) && !args.Equals("config", StringComparison.OrdinalIgnoreCase)) return;
            
            Gui.ConfigWindow.Visible = !Gui.ConfigWindow.Visible;
        }

        public static void Log(string message, string detailMessage = "")
        {
            var msg = $"[{Name}] {message}";
            PluginLog.Info(detailMessage == "" ? msg : detailMessage);
            ChatGui.Print(msg);
        }
        public static void LogError(string message, string detailMessage = "")
        {
            //if (!Config.PrintError) return;
            var msg = $"[{Name}] {message}";
            PluginLog.Error(detailMessage == "" ? msg : detailMessage);
            ChatGui.PrintError(msg);
        }
    }

}
