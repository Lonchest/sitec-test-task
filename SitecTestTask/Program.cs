using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Xml;

namespace SitecTestTask;

internal class Program
{
    private static readonly HashSet<int> excludedLevels = new HashSet<int> { 9, 11, 12, 17 };
    private const string fiasUrl = "http://fias.nalog.ru/WebServices/Public/GetLastDownloadFileInfo";
    private const string xmlAddressFilePattern = "AS_ADDR_OBJ_*.XML";
    private const string UnzipFolderName = "fias_delta_xml";
    public class Fias
    {
        public string? GarXMLDeltaURL { get; set; }
        [JsonConverter(typeof(JsonDateConverter))]
        public DateOnly Date { get; set; }
    }

    public class AddressObject
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public AddressObject(string name, string type)
        {
            this.Name = name;
            this.Type = type;
        }
    }

    static Dictionary<int, string> GetLevels()
    {
        var levelsFilePath = Directory.GetFiles(UnzipFolderName, "AS_OBJECT_LEVELS_*.XML").FirstOrDefault();
        if (levelsFilePath == null)
        {
            throw new Exception("Ошибка десериализации данных ФИАС");
        }
        var levelsDict = new Dictionary<int, string>();
        using (XmlReader reader = XmlReader.Create(levelsFilePath))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "OBJECTLEVEL")
                {
                    var level = reader.GetAttribute("LEVEL");
                    var name = reader.GetAttribute("NAME");
                    if (level == null || name == null)
                        continue;
                    levelsDict[int.Parse(level)] = name;
                }
            }
        }
        return levelsDict;
    }

    static void UnzipFile(string source)
    {
        if (Directory.Exists(UnzipFolderName))
            Directory.Delete(UnzipFolderName, true);
        ZipFile.ExtractToDirectory(source, UnzipFolderName);
    }

    static void GenerateHtmlReport(
        Dictionary<int, string> levels,
        Dictionary<int, List<AddressObject>> adressesByLevel,
        DateOnly reportDate)
    {
        var hrmlRepotFileName = "report.html";
        using (StreamWriter writer = new StreamWriter(hrmlRepotFileName))
        {
            writer.WriteLine($"<html><body>");
            writer.WriteLine($"<h1>Отчет по добавленным адресам за {reportDate}</h1>");
            writer.WriteLine("<style> " +
                "table { width: 100%; border-collapse: collapse; }" +
                "th, td { border: 1px solid black; padding: 8px; text-align: left; }" +
                "th { background-color: #f2f2f2; }</style>");

            foreach (var keyValPair in adressesByLevel)
            {
                writer.WriteLine("</table>");
                writer.WriteLine($"<h2>{levels[keyValPair.Key]}</h2>");
                writer.WriteLine("<table>");
                writer.WriteLine("<tr><th>Тип объекта</th><th>Наименование</th></tr>");

                foreach (var adress in keyValPair.Value.OrderBy(x => x.Name))
                {
                    writer.WriteLine($"<tr><td>{adress.Type}</td><td>{adress.Name}</td></tr>");
                }
                writer.WriteLine("</table>");
            }

            writer.WriteLine("</body></html>");
        }
        Console.WriteLine($"Отчет сформирован: {Path.GetFullPath(hrmlRepotFileName)}");
    }
    static Dictionary<int, List<AddressObject>> GetAdressesByLevelFromFile()
    {
        var adressesByLevel = new Dictionary<int, List<AddressObject>>();

        foreach (var directory in Directory.GetDirectories(UnzipFolderName, "*", SearchOption.AllDirectories))
        {
            var xmlFile = Directory.GetFiles(directory, xmlAddressFilePattern).FirstOrDefault();
            if (xmlFile == null)
                continue;

            using (XmlReader reader = XmlReader.Create(xmlFile))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "OBJECT")
                    {
                        if (reader.GetAttribute("ISACTIVE") == "1")
                        {
                            var lvl = reader.GetAttribute("LEVEL");
                            var name = reader.GetAttribute("NAME");
                            var type = reader.GetAttribute("TYPENAME");

                            if (lvl == null || name == null || type == null)
                                continue;
                            var lvlInt = int.Parse(lvl);
                            if (excludedLevels.Contains(lvlInt))
                                continue;
                            if (!adressesByLevel.ContainsKey(lvlInt))
                                adressesByLevel[lvlInt] = new List<AddressObject>();

                            adressesByLevel[lvlInt].Add(new AddressObject(name, type));
                        }
                    }
                }
            }
        }
        return adressesByLevel;
    }
    static async Task<Fias> GetFiasData()
    {
        var jsonResponse = "";
        using (HttpClient client = new HttpClient())
        {
            jsonResponse = await client.GetStringAsync(fiasUrl);
        }
        return JsonSerializer.Deserialize<Fias>(jsonResponse) ?? throw new JsonException("Ошибка десериализации данных ФИАС");
    }
    static async Task Main(string[] args)
    {
        var fileName = "";
        var fiasData = await GetFiasData();
        if (string.IsNullOrEmpty(fiasData.GarXMLDeltaURL))
        {
            Console.WriteLine("Не удалось получить ссылку на обновление ФИАС.");
            return;
        }
        fileName = $"fias-{fiasData.Date}.zip";

        if (!File.Exists(fileName))
        {
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(fiasData.GarXMLDeltaURL))
            using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }
            Console.WriteLine("Файл с данными загружен");
        }
        else
        {
            Console.WriteLine("Самый актуальный файл уже был загружен ранее");
        }

        if (!Directory.Exists(UnzipFolderName))
        {
            UnzipFile(fileName);
            Console.WriteLine("Данные разархивированы");
        }
        else
        {
            Console.WriteLine("Будут использоваться ранее разархивированные данные");
        }
        var dictOfLevels = GetLevels();

        var dictOfAdresses = GetAdressesByLevelFromFile();
        GenerateHtmlReport(dictOfLevels, dictOfAdresses, fiasData.Date);
    }
}