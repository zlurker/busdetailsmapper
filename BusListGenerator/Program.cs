using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusListGenerator
{
    class Program
    {
        const string apiKey = "f1c9f6b9a5db48468a2e1852568f2016";
        static HttpClient client;

        static void SetUpClient()
        {
            client = new HttpClient();
            client.BaseAddress = new Uri("http://13.76.242.0/smrtp/rest/api/v1/");
            client.DefaultRequestHeaders.Add("Authorization", "ApiKey " + apiKey);
        }

        static async Task<string> FireClient(string path)
        {
            HttpResponseMessage result = await client.GetAsync(path);
            string content = await result.Content.ReadAsStringAsync();
            return content;
        }


        static void Main(string[] args)
        {
            SetUpClient();

            string inputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bus Numbers");
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output IP");

            if (!Directory.Exists(inputPath))
                Directory.CreateDirectory(inputPath);

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            DirectoryInfo busInfo = new DirectoryInfo(inputPath);
            IEnumerable<FileInfo> csvFiles = busInfo.EnumerateFiles("*.csv");
            Console.WriteLine("Files found");

            foreach (FileInfo csvFile in csvFiles)
            {
                string[] contents = File.ReadAllText(csvFile.FullName).Split('\n');
                string outputFileName = Path.Combine(outputPath, csvFile.Name);
                StringBuilder builder = new StringBuilder();


                for (int i = 0; i < contents.Length; i++)
                {
                    Tuple<bool, string> result = QuerySCMS(contents[i]);

                    if (result.Item1)
                    {
                        builder.Append(result.Item2);
                        builder.Append("\n");
                    }
                }

                File.WriteAllText(outputFileName, builder.ToString());
            }

            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        static Tuple<bool, string> QuerySCMS(string busId)
        {
            JObject assetInfoQuery = JObject.Parse(FireClient($"asset?regnumber={ busId}").Result);
            Console.WriteLine("Checking bus ID {0} details", busId);

            if (assetInfoQuery.GetValue("size").ToString() == "0")
            {
                Console.WriteLine($"Bus number {busId} does not exist.");
                return new Tuple<bool, string>(false, "");
            }

            string assetId = assetInfoQuery.GetValue("assetList")[0]["id"].ToString();
            Console.WriteLine($"AssetId: {assetId}");

            JObject unitIdInfoQuery = JObject.Parse(FireClient($"unit?assetid={assetId}").Result);

            if (unitIdInfoQuery.GetValue("size").ToString() == "0")
            {
                Console.WriteLine($"No unit has asset ID {assetId}");
                return new Tuple<bool, string>(false, "");
            }

            string unitId = unitIdInfoQuery.GetValue("unitList")[0]["id"].ToString();
            Console.WriteLine($"UnitId: {unitId}");

            JObject unitInfoQuery = JObject.Parse(FireClient($"unit/{unitId}").Result);
            JToken ipToken;

            if (!unitInfoQuery.TryGetValue("ip", out ipToken))
            {
                Console.WriteLine($"No device has unit ID {unitId}");
                return new Tuple<bool, string>(false, "");
            }

            string ipAddress = ipToken.ToString();

            Console.WriteLine($"{busId} - {ipAddress}");
            Console.WriteLine();
            Console.WriteLine();
            return new Tuple<bool, string>(true, ipAddress);
        }
    }
}
