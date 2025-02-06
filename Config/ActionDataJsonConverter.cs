using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace MusicBeePlugin.Config
{
    public class ActionDataJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BaseActionData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            var actionType = jObject["Action"]?.Value<string>();

            if (actionType == null)
            {
                throw new JsonSerializationException("Missing action type");
            }

            var targetTypeName = $"{typeof(BaseActionData).Namespace}.{actionType}ActionData";
            var type = typeof(BaseActionData).Assembly.GetType(targetTypeName);

            if (type == null)
            {
                throw new JsonSerializationException($"Unknown action type: {actionType}");
            }

            var result = Activator.CreateInstance(type);
            serializer.Populate(jObject.CreateReader(), result);
            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = value.GetType();
            var actionName = type.Name.Replace("ActionData", "");
            var jObject = JObject.FromObject(value, serializer);
            jObject["Action"] = actionName;
            jObject.WriteTo(writer);
        }
    }
} 