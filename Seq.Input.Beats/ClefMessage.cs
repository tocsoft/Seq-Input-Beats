using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Seq.Input.Beats
{
    public class ClefMessage
    {
        public DateTime UtcTimestamp { get; set; }

        public string? Exception { get; set; }

        public string MessageTemplate { get; set; }

        public string Message { get; set; }

        public string? Level { get; set; }

        Dictionary<string, object> _feilds;
        public IDictionary<string, object> Properties => _feilds ?? (_feilds = new Dictionary<string, object>());

        public void Write(JsonTextWriter writer)
        {
            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(Message))
            {
                writer.WritePropertyName("@m");
                writer.WriteValue(Message);
            }

            if (!string.IsNullOrEmpty(MessageTemplate))
            {
                writer.WritePropertyName("@mt");
                writer.WriteValue(MessageTemplate);
            }

            if (!string.IsNullOrEmpty(Level))
            {
                writer.WritePropertyName("@l");
                writer.WriteValue(Level);
            }

            if (!string.IsNullOrEmpty(Exception))
            {
                writer.WritePropertyName("@x");
                writer.WriteValue(Exception);
            }

            writer.WritePropertyName("@t");
            writer.WriteValue(UtcTimestamp.ToString("o", CultureInfo.InvariantCulture));

            if (_feilds != null)
            {
                foreach (var f in _feilds)
                {
                    var name = f.Key;
                    if (name.StartsWith("@"))
                    {
                        name = '@' + name;
                    }

                    writer.WritePropertyName(name);
                    writer.WriteValue(f.Value);
                }
            }

            writer.WriteEndObject();
            writer.Flush();
        }
    }
}
