using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mentalo.Persistance
{
    /// <summary>Le JSON de Mental-O sur .NET 8 (26/10/2026) : System.Text.Json
    /// réglé pour relire ce que JavaScriptSerializer écrivait (noms de
    /// propriétés tels quels, nombres tolérants, nulls omis — les DTO .tea
    /// sont nullables là où l'ancien lecteur tolérait l'absence).</summary>
    public static class Json
    {
        private static readonly JsonSerializerOptions Options = Construire();

        private static JsonSerializerOptions Construire()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = null;
            options.PropertyNameCaseInsensitive = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.IncludeFields = false;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            options.AllowTrailingCommas = true;
            return options;
        }

        public static string Serialiser<T>(T objet)
        {
            return JsonSerializer.Serialize(objet, Options);
        }

        public static T Deserialiser<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default(T);
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>Lecture souple : un arbre de Dictionary/List/string/double/bool,
        /// comme DeserializeObject de l'ancien JavaScriptSerializer (JSON Canvas).</summary>
        public static object LireSouple(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }))
                return Convertir(document.RootElement);
        }

        private static object Convertir(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> objet = new Dictionary<string, object>();
                    foreach (JsonProperty propriete in element.EnumerateObject())
                        objet[propriete.Name] = Convertir(propriete.Value);
                    return objet;
                case JsonValueKind.Array:
                    List<object> liste = new List<object>();
                    foreach (JsonElement enfant in element.EnumerateArray())
                        liste.Add(Convertir(enfant));
                    return liste.ToArray();
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    double nombre;
                    return element.TryGetDouble(out nombre) ? (object)nombre : element.GetRawText();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }
    }
}
