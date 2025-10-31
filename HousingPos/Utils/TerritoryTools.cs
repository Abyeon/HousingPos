using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using HousingPos.Utils.Structs;
using Lumina.Excel.Sheets;
using Lumina.Extensions;

namespace HousingPos.Utils;

public static unsafe class TerritoryTools
{
    private static readonly Dictionary<uint, string> HousingDistricts = new()
    {
        {502, "Mist"},
        {505, "Goblet"},
        {507, "Lavender Beds"},
        {512, "Empyreum"},
        {513, "Shirogane"}
    };

    private static readonly Dictionary<string, string> HousingRenovations = new()
    {
        { "Riviera Style", "Mist" },
        { "Glade Style", "Lavender Beds" },
        { "Oasis Style", "Goblet" },
        { "Far Eastern Style", "Shirogane" },
        { "Highland Style", "Empyreum" },
        { "Minimalist Style", "Minimalist" }
    };
    
    private static uint CorrectedTerritoryTypeId
    { 
        get
        {
            var manager = HousingManager.Instance();
            if (manager == null)
            {
                return HousingPos.ClientState.TerritoryType;
            }
    
            var character = HousingPos.ClientState.LocalPlayer;
            if (character == null || manager->CurrentTerritory == null) return HousingPos.ClientState.TerritoryType;
            
            var territoryType = manager->IndoorTerritory != null
                ? ((HousingTerritory2*)manager->CurrentTerritory)->TerritoryTypeId
                : HousingPos.ClientState.TerritoryType;

            return territoryType;

        }
    }

    public static string GetDistrict()
    {
        var row = HousingPos.Data.GetExcelSheet<TerritoryType>().GetRow(CorrectedTerritoryTypeId).PlaceNameZone.RowId;
        HousingPos.Log(row.ToString());
        return HousingDistricts.GetValueOrDefault(row, row.ToString());
    }

    public static string GetRenovation()
    {
        var id = GetTerritoryId();
        var renovation = HousingPos.Data.GetExcelSheet<HousingRenovation>().FirstOrNull(row => row.Unknown1 == id);
        return renovation == null ? "" : renovation.Value.Name.ExtractText();
    }

    public static string GetMakePlaceRenovation()
    {
        var reno = GetRenovation();
        return reno == "" ? "" : HousingRenovations[reno];
    }

    public static uint GetTerritoryId()
    {
        var man = AgentMap.Instance();
        var id = man->CurrentTerritoryId;
        HousingPos.Log(id.ToString());
        return man->CurrentTerritoryId;
    }

    public static string GetHouseSize()
    {
        var man = HousingManager.Instance();
        
        if (!man->IsInside()) return "";
        if (man->GetCurrentRoom() != 0) return "Apartment";
        
        var id = GetTerritoryId();
        var type = HousingPos.Data.GetExcelSheet<HousingIndoorTerritory>().GetRow(id).Unknown0;

        return type switch
        {
            0 => "Small",
            1 => "Medium",
            2 => "Large",
            3 => "Apartment",
            4 => "Unknown0",
            5 => "Unknown1",
            255 => "Unknown2",
            _ => ""
        };
    }
}