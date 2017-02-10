using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DapperExtensions;
using VaShare.Contract.Data.Product;
using VaShare.Core.IO;

namespace USACityUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            UsaUploader.Start();
        }
    }

    public static class BaseConfig
    {
        public const string connectionString =
            "Server=10.2.3.19;Port=3306;Database=Product;Uid=vashare;Pwd=123456;charset=utf8;";
    }

    public static class UsaUploader
    {
        private static MySqlClient client = new MySqlClient(BaseConfig.connectionString);

        private static string path = "E:\\RCIDATA1";
        private static int countryID = 236;
        public static void Start()
        {
            var resorts = GetResorts();

            foreach (var resort in resorts)
            {
                try
                {
                    UpLoad(resort);
                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine(resort.RCICode + "完成");

            }
            Console.ReadKey();
        }

        private static List<RegionWithName> GetUSACitys()
        {
            string sql = $@"SELECT
	`r`.`ID` AS `ID`,
	`ut`.`En` AS `Name`,
	`r`.`CreateTime` AS `CreateTime`,
	`r`.`RowVersion` AS `RowVersion`,
	`r`.`ParentRegionID` AS `ParentRegionID`,
	`r`.`EnumEntityStatus` AS `EnumEntityStatus`,
	`r`.`EnumRegionLevel` AS `EnumRegionLevel`,
	`r`.`CountryID` AS `CountryID`,
	`r`.`Longitude` AS `Longitude`,
	`r`.`Latitude` AS `Latitude`
FROM
	`Region` `r`
JOIN `UserText` `ut` ON `r`.`UserTextID` = `ut`.`ID`
WHERE r.EnumRegionLevel=4 AND CountryID={countryID}";
            var tmp = client.Query<RegionWithName>(sql).ToList();
            return tmp;
        }

        private static List<ResortsWithCode> GetResorts()
        {
            string sql =
                $@"SELECT R.*,RBI.ExternalResortCode AS RCICode FROM Resort R INNER JOIN ResortBusinessInfo RBI ON R.ID=RBI.ResortID WHERE R.CountryID={countryID}";
            var tmp = client.Query<ResortsWithCode>(sql).ToList();
            return tmp;
        }


        private static void UpLoad(ResortsWithCode resort)
        {
            var file = $"{path}\\{resort.RCICode}\\address.json";
            if (!File.Exists(file))
            {
                Console.WriteLine(resort.RCICode + "不存在地址信息");
            }

            var json = File.ReadAllText(file);
            string localityName = null;
            string area2Name = null;
            string stateName = null;
            var googleAddress = JsonHelper.JsonToObject(json, typeof(GoogleMapResults)) as GoogleMapResults;
            if (googleAddress.results.Count > 0 && googleAddress.results[0].address_components.Length > 0)
            {
                foreach (var component in googleAddress.results[0].address_components)
                {
                    if (component.types.Exists(p => p == "locality"))
                    {
                        localityName = component.long_name;
                    }
                    else if (component.types.Exists(p => p == "administrative_area_level_2"))
                    {
                        area2Name = component.long_name;
                    }
                    else if (component.types.Exists(p => p == "administrative_area_level_1"))
                    {
                        stateName = component.long_name;
                    }

                }
            }

            string regionSql = @"SELECT * FROM(SELECT
	r.*, ut.En AS Name,
	ut1.En AS ParentName
FROM
	Region r
JOIN Region parent ON r.ParentRegionID = parent.ID
JOIN UserText ut ON r.UserTextID = ut.ID
JOIN UserText ut1 ON parent.UserTextID = ut1.ID
WHERE
	r.EnumRegionLevel = 4
AND r.CountryID = {2})A where ParentName='{0}' AND Name='{1}'";

            if (string.IsNullOrEmpty(stateName))
            {
                Console.WriteLine(resort.RCICode + "没有找到对应的state");
                return;
            }
            var curCity = client.Query<RegionWithName>(string.Format(regionSql, stateName, localityName, countryID)).FirstOrDefault();
            if (curCity == null)
            {
                var county = client.Query<RegionWithName>(string.Format(regionSql, stateName, area2Name, countryID)).FirstOrDefault();
                if (county == null)
                {
                    Console.WriteLine($"{resort.RCICode}没有找到地址信息{localityName}和{area2Name}");
                    resort.CityID = 0;
                }
                else
                {
                    var userText = new UserText();
                    userText.SourceText = userText.En = localityName;
                    userText.EnumUserTextCategory = EnumUserTextCategory.Region;
                    var userTextID = client.Insert(userText);
                    var region = new Region();
                    region.ParentRegionID = county.ParentRegionID;
                    region.EnumRegionLevel = EnumRegionLevel.City;
                    region.CountryID = countryID;
                    region.UserTextID = userTextID;
                    var regionID = client.Insert(region);
                    resort.CityID = regionID;
                }
            }
            else
            {
                resort.CityID = curCity.ID;
            }


            string sql = $"update Resort set CityID={resort.CityID} where ID={resort.ID}";
            var result = client.Execute(sql);
        }
    }

    public class RegionWithName : Region
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
    }

    public class ResortsWithCode : Resort
    {
        public string RCICode { get; set; }
    }
}
