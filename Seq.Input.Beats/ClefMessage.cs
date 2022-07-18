using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Seq.Input.Beats
{
    public class ClefMessage
    {
        public string? UtcTimestamp { get; set; }

        public string? Exception { get; set; }

        public string MessageTemplate { get; set; }

        public string Message { get; set; }

        public string? Level { get; set; }

        Dictionary<string, object> _feilds;
        public IDictionary<string, object> Properties => _feilds ?? (_feilds = new Dictionary<string, object>());

        public void Write(TextWriter writer)
        {
            using var jsonWriter = new JsonTextWriter(writer);
            jsonWriter.Formatting = Formatting.None;
            jsonWriter.Indentation = 0;

            jsonWriter.WriteStartObject();

            if (!string.IsNullOrEmpty(Message))
            {
                jsonWriter.WritePropertyName("@m");
                jsonWriter.WriteValue(Message);
            }

            if (!string.IsNullOrEmpty(MessageTemplate))
            {
                jsonWriter.WritePropertyName("@mt");
                jsonWriter.WriteValue(MessageTemplate);
            }

            if (!string.IsNullOrEmpty(Level))
            {
                jsonWriter.WritePropertyName("@l");
                jsonWriter.WriteValue(Level);
            }

            if (!string.IsNullOrEmpty(Exception))
            {
                jsonWriter.WritePropertyName("@x");
                jsonWriter.WriteValue(Exception);
            }

            var timestamp = UtcTimestamp;
            if (DateTime.TryParse(timestamp, out var dt))
            {
                timestamp = dt.ToString("o", CultureInfo.InvariantCulture);
            }
            else
            {
                timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            }

            jsonWriter.WritePropertyName("@t");
            jsonWriter.WriteValue(timestamp);

            if (_feilds != null)
            {
                foreach (var f in _feilds)
                {
                    var name = f.Key;
                    if (name.StartsWith("@"))
                    {
                        name = '@' + name;
                    }

                    jsonWriter.WritePropertyName(name);
                    jsonWriter.WriteValue(f.Value);
                }
            }

            jsonWriter.WriteEndObject();
            jsonWriter.Flush();
        }
    }
}
