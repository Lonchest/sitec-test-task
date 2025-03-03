using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SitecTestTask;

internal class JsonDateConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "dd.MM.yyyy";
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateStr = reader.GetString();

        if (DateOnly.TryParseExact(dateStr, DateFormat, out var date))
        {
            return date;
        }
        throw new JsonException($"Неверный формат даты в JSON. Нужен: {DateFormat}");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat));
    }
}
