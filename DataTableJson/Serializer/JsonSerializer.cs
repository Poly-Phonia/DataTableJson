using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataTableJson.Serializer
{
    /// <summary>
    /// JSONへのシリアライズを実行する
    /// </summary>
    public class JsonSerializer
    {
        /// <summary>
        /// シリアライズを実行する
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public string Serialize(SerializeProfile profile, DataTable table)
        {
            return Serialize(profile, table, System.Text.Json.JsonSerializerOptions.Default);
        }
        
        /// <summary>
        /// シリアライズを実行する
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public string Serialize(SerializeProfile profile, DataTable table, System.Text.Json.JsonSerializerOptions options)
        {
            var dictionaries = profile.ConvertToDictionaries(table);

            var json = System.Text.Json.JsonSerializer.Serialize(dictionaries, options);

            return json;
        }
    }
}
