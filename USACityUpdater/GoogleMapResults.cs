using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace USACityUpdater
{
    public class GoogleMapResults
    {
        public List<MapResult> results { get; set; } = new List<MapResult>();
        public string status { get; set; }
    }

    public class MapResult
    {
        public address_component[] address_components { get; set; }

        public string formatted_address { get; set; }
    }

    public class address_component
    {
        public string long_name { get; set; }
        public string short_name { get; set; }
        public List<string> types { get; set; }
    }

    public static class JsonHelper
    {
        // 从一个对象信息生成Json串
        public static string ObjectToJson(object obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, obj);
            byte[] dataBytes = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(dataBytes, 0, (int)stream.Length);
            return Encoding.UTF8.GetString(dataBytes);
        }
        // 从一个Json串生成对象信息
        public static object JsonToObject(string jsonString, Type type)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(type);
            MemoryStream mStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            return serializer.ReadObject(mStream);
        }

    }
}