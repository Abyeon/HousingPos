using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.IoC;
using Dalamud.Plugin;
using HousingPos.Objects;

namespace HousingPos
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public HousingPosLanguage HousingPosLanguage = HousingPosLanguage.Client;
        public bool ShowTooltips = true;
        public bool Previewing = false;
        public bool DrawScreen = false;
        public float DrawDistance = 0;
        public List<int> HiddenScreenItemHistory = [];
        public List<int> GroupingList = [];
        public bool Grouping = false;
        public string UILanguage = "en";
        public List<HousingItem> HousingItemList = [];
        public List<string> Tags = [];

        public bool MakePlaceFormatting = true;
        public string SaveLocation = "";
        
        public bool BDTH = false;
        public bool SingleExport = false;
        public int SelectedItemIndex = -1;
        public float PlaceX = 0;
        public float PlaceY = 0;
        public float PlaceZ = 0;
        public float PlaceRotate = 0;
        public DateTime lastPosPackageTime = DateTime.Now;
        #region Init and Save

        public void Save()
        {
            HousingPos.Interface.SavePluginConfig(this);
        }

        public void ResetRecord()
        {
            PlaceX = 0;
            PlaceY = 0;
            PlaceZ = 0;
            PlaceRotate = 0;
            SelectedItemIndex = -1;
            HiddenScreenItemHistory.Clear();
            GroupingList.Clear();
            Save();
        }

        #endregion
    }
}