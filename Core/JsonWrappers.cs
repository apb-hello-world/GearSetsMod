using Newtonsoft.Json;

namespace GearSetsMod.Core
{
    public interface IJsonWrapper
    {
        string ToJson(object obj);
        T FromJson<T>(string json);
    }

    public class StandardJsonWrapper : IJsonWrapper
    {
        public string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        public T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
