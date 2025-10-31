using System;
using System.Collections.Generic;
using System.Numerics;
using HousingPos.Objects;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace HousingPos.Utils;

public static class MakePlaceConverter
{
    public static Quaternion FromEuler(float rotate)
    {
        // The Python code `R.from_euler('xyz', [0, 0, -rotate], False)` uses radians,
        // as specified by the `False` parameter. The order is 'xyz'.
        float roll = 0;
        float pitch = 0;
        float yaw = -rotate;

        // The System.Numerics.Quaternion conversion formula from Euler angles (in radians).
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
    
    private class MPTransform(Vector3 location, Quaternion rotation, Vector3 scale)
    {
        public float[] location = [location.X,  location.Y, location.Z];
        public float[] rotation = [rotation.X, rotation.Y, rotation.Z, rotation.W];
        public float[] scale = [scale.X, scale.Y, scale.Z];
    }

    private static string RgbHexFromUint32(uint color)
    {
        uint rgb = (color & 0x00FFFFFF);
        string hex = rgb.ToString("X6");
        return hex == "000000" ? "" : hex;
    }

    private class MPProperties(uint color)
    {
        public string color = RgbHexFromUint32(HousingPos.Data.GetExcelSheet<Stain>().GetRow(color).Color);
    }
    
    private class MPFurniture(uint itemId, string name, MPTransform transform, MPProperties properties)
    {
        public uint itemId = itemId;
        public string name = name;
        public MPTransform transform = transform;
        public MPProperties properties = properties;
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
                    new Vector3(item.X * 100, item.Z * 100, item.Y * 100),
                    FromEuler(item.Rotate), 
                    Vector3.One), 
                new MPProperties(item.Stain));
            
            interiorFurniture.Add(mpFurn);
        }
        
        var interior = JsonConvert.SerializeObject(interiorFurniture);
        return $"{{\"lightLevel\":1,\"houseSize\":\"{houseSize}\",\"interiorFixture\":[{{\"level\":\"\",\"type\":\"District\",\"name\":\"{houseName}\",\"itemId\":0,\"color\":\"\"}},],\"metaData\":{{\"version\":139}},\"interiorScale\":100,\"interiorFurniture\":{interior},\"properties\":{{}}}}";
    }
}