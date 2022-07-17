using System;
using System.Collections.Generic;
using System.Linq;

namespace Seq.Input.Beats
{
    public class Message
    {
        public Message(int sequence, int fieldLength)
        {
            Fields = new Dictionary<string, string>(fieldLength);
            Sequence = sequence;
        }

        public Message(int sequence, Dictionary<string, string> fields)
        {
            Fields = fields ?? new Dictionary<string, string>();
            Sequence = sequence;
        }

        public Dictionary<string, string> Fields { get; }

        public int Sequence { get; }

        private string GetField(string name)
        {
            if (Fields.TryGetValue(name, out var v))
            {
                return v;
            }
            return null;
        }
        private static string[] SpecialFields = new[]
        {
            "message",
            "@timestamp"
        };
        public ClefMessage AsResult()
        {
            var r = new ClefMessage()
            {
                UtcTimestamp = DateTime.Parse(GetField("@timestamp")),
                Message = GetField("message"),
            };

            foreach (var f in Fields)
            {
                if (SpecialFields.Contains(f.Key))
                {
                    continue;
                }

                r.Properties.Add(f.Key, f.Value);
            }

            return r;
        }
    }
}
