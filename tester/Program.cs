using CsvHelper;
using System.Data;

namespace tester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //テスト用CSVを読む
            DataTable sheet1 = new DataTable();
            using(var reader = new StreamReader("sheet1.csv"))
            {
                using(var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    using(var dr = new CsvDataReader(csv))
                    {
                        sheet1.Load(dr);
                    }
                }
            }

            DataTable sheet2 = new DataTable();
            using (var reader = new StreamReader("sheet2.csv"))
            {
                using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    using (var dr = new CsvDataReader(csv))
                    {
                        sheet2.Load(dr);
                    }
                }
            }

            DataTable sheet3 = new DataTable();
            using (var reader = new StreamReader("sheet3.csv"))
            {
                using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                {
                    using (var dr = new CsvDataReader(csv))
                    {
                        sheet3.Load(dr);
                    }
                }
            }

            //プロファイル作成
            var profile = new DataTableJson.Serializer.SerializeProfile();
            profile.AddTable(sheet1);
            profile.AddTable(sheet2);
            profile.AddTable(sheet3);

            var join1 = new DataTableJson.Serializer.TableRelationArray(sheet1, sheet2, "Join1");
            join1.AddJoinColumn(sheet1.Columns["Code"], sheet2.Columns["MainCode"]);
            profile.AddRelation(join1);

            var join2 = new DataTableJson.Serializer.TableRelationSingle(sheet2, sheet3, "Join2");
            join2.AddJoinColumn(sheet2.Columns["Code"], sheet3.Columns["TitleCode"]);
            profile.AddRelation(join2);

            //json
            var serializer = new DataTableJson.Serializer.JsonSerializer();
            var option = new System.Text.Json.JsonSerializerOptions();
            option.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
            option.WriteIndented = true;
            var json = serializer.Serialize(profile, sheet1, option);
            Console.WriteLine(json);
        }
    }
}
