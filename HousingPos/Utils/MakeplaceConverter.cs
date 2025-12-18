using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using HousingPos.Objects;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HousingPos.Utils;

public static class MakePlaceConverter
{
    public static Quaternion FromEuler(float rotate)
    {
        float roll = 0;
        float pitch = 0;
        float yaw = -rotate;
        
        float cy = (float)Math.Cos(yaw * 0.5);
        float sy = (float)Math.Sin(yaw * 0.5);
        float cp = (float)Math.Cos(pitch * 0.5);
        float sp = (float)Math.Sin(pitch * 0.5);
        float cr = (float)Math.Cos(roll * 0.5);
        float sr = (float)Math.Sin(roll * 0.5);

        return new Quaternion(
            x: (sr * cp * cy - cr * sp * sy),
            y: (cr * sp * cy + sr * cp * sy),
            z: (cr * cp * sy - sr * sp * cy),
            w: (cr * cp * cy + sr * sp * sy)
        );
    }

    public static Vector3 ToEulerAngles(Quaternion q)
    {
        Vector3 angles = new();

        // roll / x
        double sinrCosp = 2 * (q.W * q.X + q.Y * q.Z);
        double cosrCosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        angles.X = (float)Math.Atan2(sinrCosp, cosrCosp);

        // pitch / y
        double sinp = 2 * (q.W * q.Y - q.Z * q.X);
        if (Math.Abs(sinp) >= 1)
        {
            angles.Y = (float)Math.CopySign(Math.PI / 2, sinp);
        }
        else
        {
            angles.Y = (float)Math.Asin(sinp);
        }

        // yaw / z
        double sinyCosp = 2 * (q.W * q.Z + q.X * q.Y);
        double cosyCosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        angles.Z = (float)Math.Atan2(sinyCosp, cosyCosp);

        return angles;
    }
    
    private class MPTransform
    {
        // ReSharper disable InconsistentNaming
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        public List<float> location;
        public List<float> rotation;
        public List<float> scale;

        public MPTransform(Vector3 location, Quaternion rotation, Vector3 scale)
        {
            this.location = [location.X,  location.Y, location.Z];
            this.rotation = [rotation.X, rotation.Y, rotation.Z, rotation.W];
            this.scale = [scale.X, scale.Y, scale.Z];
        }

        [JsonConstructor]
        public MPTransform(float[] location, float[] rotation, float[] scale)
        {
            this.location = location.ToList();
            this.rotation = rotation.ToList();
            this.scale = scale.ToList();
        }
    }

    private static string RgbHexFromUint32(uint color)
    {
        uint rgb = (color & 0x00FFFFFF);
        string hex = rgb.ToString("X6");
        return hex == "000000" ? "" : hex;
    }

    private static uint Uint32FromHex(string hex)
    {
        return uint.Parse(hex, NumberStyles.HexNumber);
    }

    private static int ColorDiff(Color c1, Color c2)
    {
        return (int)Math.Sqrt((c1.R - c2.R) * (c1.R - c2.R)
                              + (c1.G - c2.G) * (c1.G - c2.G)
                              + (c1.B - c2.B) * (c1.B - c2.B));
    }

    private static uint ClosestColor(Color rgba)
    {
        var colorList = HousingPos.Data.GetExcelSheet<Stain>();
        
        var minDistance = 2000;
        var closestColor = colorList[0];

        foreach (var color in colorList)
        {
            if (!color.IsHousingApplicable) continue;
            
            uint value = color.Color;
            var colorRgba = Color.FromArgb((int)value);
            
            int distance = ColorDiff(rgba, colorRgba);
            if (!(distance < minDistance)) continue;

            minDistance = distance;
            closestColor = color;
        }
        
        return closestColor.RowId;
    }

    private class MPProperties
    {
        public string? color;

        public MPProperties(uint color)
        {
            this.color = RgbHexFromUint32(HousingPos.Data.GetExcelSheet<Stain>().GetRow(color).Color);
        }
        
        [JsonConstructor]
        public MPProperties(string color)
        {
            this.color = color;
        }
    }
    
    private class MPFurniture(uint itemId, string name, MPTransform transform, MPProperties properties)
    {
        public uint itemId = itemId;
        public string name = name;
        public MPTransform transform = transform;
        public MPProperties properties = properties;
    }

    public static void ConvertFromMakePlace(string data, ref List<HousingItem> furniture)
    {
        try
        {
            var json = JObject.Parse(data);
            if (json["interiorFurniture"] == null) return;

            var scale = 0.01f;
            if (json["interiorScale"] != null)
            {
                scale = 1 / (float)json["interiorScale"]!;
            }
            
            var mpObjects = json["interiorFurniture"]!.ToObject<List<MPFurniture>>();

            if (mpObjects is not { Count: > 0 }) return;
            furniture.Clear();
            
            foreach (var mpObject in mpObjects)
            {
                var furnishingRow = HousingPos.Data.GetExcelSheet<HousingFurniture>()
                    .FirstOrNull(x => x.Item.Value.RowId == mpObject.itemId);
                
                if (furnishingRow == null) continue;
                
                var rotation = new Quaternion(mpObject.transform.rotation[0], mpObject.transform.rotation[1], mpObject.transform.rotation[2], mpObject.transform.rotation[3]);
                var euler = ToEulerAngles(rotation);
                var color = string.IsNullOrEmpty(mpObject.properties.color) ? 0 :
                    ClosestColor(ColorTranslator.FromHtml("#" + mpObject.properties.color[..6]));

                var newItem = new HousingItem(
                    furnishingRow.Value.RowId,
                    furnishingRow.Value.ModelKey,
                    mpObject.itemId,
                    color,
                    mpObject.transform.location[0] * scale,
                    mpObject.transform.location[2] * scale,
                    mpObject.transform.location[1] * scale,
                    -euler.Z,
                    furnishingRow.Value.Item.Value.Name.ToString());
                
                furniture.Add(newItem);
            }
        }
        catch (Exception e)
        {
            HousingPos.LogError(e.ToString());
        }
    }
    
    public static string GetMakePlaceJson(List<HousingItem> furniture, string houseSize, string houseName)
    {
        var interiorFurniture = new List<MPFurniture>();
        foreach (var item in furniture)
        {
            var mpFurn = new MPFurniture(
                item.ItemKey,
                item.Name, 
                new MPTransform(
                    new Vector3(item.X, item.Z, item.Y),
                    FromEuler(item.Rotate), 
                    Vector3.One), 
                new MPProperties(item.Stain));
            
            interiorFurniture.Add(mpFurn);
        }
        
        var interior = JsonConvert.SerializeObject(interiorFurniture);
        return $"{{\"lightLevel\":1,\"houseSize\":\"{houseSize}\",\"interiorFixture\":[{{\"level\":\"\",\"type\":\"District\",\"name\":\"{houseName}\",\"itemId\":0,\"color\":\"\"}}],\"metaData\":{{\"version\":139}},\"interiorScale\":1,\"interiorFurniture\":{interior},\"properties\":{{}}}}";
    }
}