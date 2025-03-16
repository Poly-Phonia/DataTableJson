using System.Collections.Generic;
using System.Data;

namespace DataTableJson.Serializer
{
    /// <summary>
    /// Jsonへのシリアライズ設定を保持する
    /// </summary>
    public class SerializeProfile
    {
        List<DataTable> dataTables;
        List<TableRelation> tableRelations;

        /// <summary>
        /// 対象となる<see cref="DataTable"/>の一覧
        /// </summary>
        public IReadOnlyList<DataTable> DataTables => dataTables.AsReadOnly();

        /// <summary>
        /// 設定された結合条件の一覧
        /// </summary>
        public IReadOnlyList<TableRelation> TableRelations => tableRelations.AsReadOnly();

        public SerializeProfile()
        {
            dataTables = new List<DataTable>();
            tableRelations = new List<TableRelation>();
        }

        /// <summary>
        /// <see cref="DataTable"/>を追加する
        /// </summary>
        /// <param name="table"></param>
        public void AddTable(DataTable table)
        {
            if (table == null)
            {
                return;
            }

            if (!dataTables.Contains(table))
            {
                dataTables.Add(table);
            }
        }

        /// <summary>
        /// <see cref="DataTable"/>を削除する。関連する<see cref="TableRelation"/>も削除される。
        /// </summary>
        /// <param name="table"></param>
        public void RemoveTable(DataTable table)
        {
            if (dataTables.Contains(table))
            {
                dataTables.Remove(table);
                var removeRelations = 
                    tableRelations.Where(r => r.ParentTable == table || r.ChildTable == table).ToList();
                foreach (var rem in removeRelations)
                {
                    tableRelations.Remove(rem);
                }
            }
        }

        /// <summary>
        /// 結合条件を追加する
        /// </summary>
        /// <param name="relation"></param>
        public void AddRelation(TableRelation relation)
        {
            if (!dataTables.Contains(relation.ParentTable) ||
               !dataTables.Contains(relation.ChildTable))
            {
                throw new ArgumentException("対象外のテーブルが含まれています。");
            }

            //循環参照しないかチェックする
            if(tableRelations.Any(r => r.ParentTable == relation.ChildTable && r.ChildTable == relation.ParentTable))
            {
                throw new ArgumentException("循環参照が発生する結合条件が指定されました。");
            }

            tableRelations.Add(relation);
        }

        /// <summary>
        /// 結合条件を削除する
        /// </summary>
        /// <param name="relation"></param>
        public void RemoveRelation(TableRelation relation)
        {
            if (tableRelations.Contains(relation))
            {
                tableRelations.Remove(relation);
            }
        }

        /// <summary>
        /// テーブルを指定し、各行をJsonシリアライズ用のDictionaryに変換する。
        /// </summary>
        /// <param name="mainTable"></param>
        /// <returns></returns>
        public List<Dictionary<string, object>> ConvertToDictionaries(DataTable mainTable)
        {
            if(mainTable == null)
            {
                throw new ArgumentNullException(nameof(mainTable));
            }

            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            foreach (var row in mainTable.Rows.Cast<DataRow>())
            {
                Dictionary<string, object> values = convertRowToDictionary(row);
                results.Add(values);
            }

            return results;
        }

        private Dictionary<string, object> convertRowToDictionary(DataRow row)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();

            //素の列を使用して値を作る
            foreach (var col in row.Table.Columns.Cast<DataColumn>())
            {
                var key = col.ColumnName;
                if (values.ContainsKey(key))
                {
                    key = $"{key}_col{col.Ordinal}";
                }

                values.Add(key, row[col]);
            }

            //結合設定を参照して処理する
            foreach(var relation in tableRelations.Where(r => r.ParentTable == row.Table))
            {
                if(relation.ReturnType == TableRelation.ReturnTypes.Single)
                {
                    //結合結果が単一
                    Dictionary<string, object> childProp;
                    var childRow = relation.GetRelationalRows(row).FirstOrDefault();
                    if (childRow == null)
                    {
                        childProp = null;
                    }
                    else
                    {
                        childProp = convertRowToDictionary(childRow);
                    }

                    var key = relation.JsonPropertyName;
                    if (values.ContainsKey(key))
                    {
                        key = $"{key}_col{values.Keys.Count + 1}";
                    }
                    values.Add(key, childProp);
                }
                else if(relation.ReturnType == TableRelation.ReturnTypes.Array)
                {
                    //結合結果が配列
                    List<Dictionary<string, object>> childProps = new List<Dictionary<string, object>>();
                    var childRows = relation.GetRelationalRows(row);
                    foreach (var childRow in childRows)
                    {
                        Dictionary<string, object> childProp = convertRowToDictionary(childRow);
                        childProps.Add(childProp);
                    }

                    var key = relation.JsonPropertyName;
                    if (values.ContainsKey(key))
                    {
                        key = $"{key}_col{values.Keys.Count + 1}";
                    }
                    values.Add(key, childProps);
                }
            }

            return values;
        }
    }

    /// <summary>
    /// <see cref="DataTable"/>同士の結合条件を保持する基底クラス
    /// </summary>
    public abstract class TableRelation
    {
        /// <summary>
        /// 結合の親となるテーブル
        /// </summary>
        public DataTable ParentTable { get; }

        /// <summary>
        /// 結合先のテーブル
        /// </summary>
        public DataTable ChildTable { get; }

        /// <summary>
        /// JSONに変換する際のプロパティ名
        /// </summary>
        public string JsonPropertyName { get; }

        /// <summary>
        /// 指定された結合条件のリスト
        /// </summary>
        public IReadOnlyList<Tuple<DataColumn, DataColumn>> ChildColumns => joinCols.AsReadOnly();

        /// <summary>
        /// 結合時の戻り値が単一か配列か示す値
        /// </summary>
        public abstract ReturnTypes ReturnType { get; }

        protected List<RelationalColumnPair> joinCols;

        internal TableRelation(DataTable parentTable, DataTable childTable, string jsonPropertyName)
        {
            ParentTable = parentTable;
            ChildTable = childTable;
            JsonPropertyName = jsonPropertyName;

            joinCols = new List<RelationalColumnPair>();
        }

        /// <summary>
        /// 結合する列を設定する
        /// </summary>
        /// <param name="parentColumn"><see cref="ParentTable"/>の結合条件列</param>
        /// <param name="childColumn"><see cref="ChildTable"/>の結合条件列</param>
        public virtual void AddJoinColumn(DataColumn parentColumn, DataColumn childColumn)
        {
            if(parentColumn == null || childColumn == null)
            {
                throw new ArgumentNullException();
            }

            if(parentColumn.Table != ParentTable)
            {
                throw new ArgumentOutOfRangeException(nameof(parentColumn), $"{nameof(ParentTable)}に所属しない列が指定されました。");
            }
            if (childColumn.Table != ChildTable)
            {
                throw new ArgumentOutOfRangeException(nameof(childColumn), $"{nameof(ChildTable)}に所属しない列が指定されました。");
            }

            if(joinCols.Any(c => c.Item1 == parentColumn && c.Item2 == childColumn))
            {
                throw new ArgumentException("指定された列の組み合わせはすでに登録されています。");
            }

            joinCols.Add(new RelationalColumnPair(parentColumn, childColumn));
        }

        /// <summary>
        /// 指定した結合列の組み合わせを削除する
        /// </summary>
        /// <param name="parentColumn"></param>
        /// <param name="childColumn"></param>
        public virtual void RemoveJoinColumn(DataColumn parentColumn, DataColumn childColumn)
        {
            var rem = joinCols.FirstOrDefault(t => t.Item1 == parentColumn && t.Item2 == childColumn);
            if (rem != null)
            {
                joinCols.Remove(rem);
            }
        }

        /// <summary>
        /// 結合される列を取得する
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public abstract IList<DataRow> GetRelationalRows(DataRow row);

        /// <summary>
        /// 戻り値の種類
        /// </summary>
        public enum ReturnTypes
        {
            /// <summary>
            /// 単一
            /// </summary>
            Single = 0,

            /// <summary>
            /// 配列
            /// </summary>
            Array = 1
        }
    }

    public class RelationalColumnPair : Tuple<DataColumn, DataColumn>
    {
        public DataColumn ParentColumn { get; }
        public DataColumn ChildColumn { get; }

        internal RelationalColumnPair(DataColumn item1, DataColumn item2) : base(item1, item2)
        {
            ParentColumn = item1;
            ChildColumn = item2;
        }

        /// <summary>
        /// 2つの列の値が一致するかチェックする
        /// </summary>
        /// <param name="parentRow"></param>
        /// <param name="childRow"></param>
        /// <returns></returns>
        public bool IsEqualRow(DataRow parentRow, DataRow childRow)
        {
            return parentRow != null && childRow != null  &&
                   !DBNull.Value.Equals(parentRow) && !DBNull.Value.Equals(childRow) &&
                   parentRow[ParentColumn] != null  && childRow[ChildColumn] != null &&
                   parentRow[ParentColumn].GetType() == childRow[ChildColumn].GetType() &&
                   parentRow[ParentColumn].Equals(childRow[ChildColumn]);
        }
    }

    /// <summary>
    /// 1:1結合を表す結合条件
    /// </summary>
    public sealed class TableRelationSingle : TableRelation
    {
        public TableRelationSingle(DataTable parentTable, DataTable childTable, string jsonPropertyName) : base(parentTable, childTable, jsonPropertyName)
        {
        }

        public override ReturnTypes ReturnType => ReturnTypes.Single;

        /// <summary>
        /// 結合される列を取得する。対象があった場合は１件のみのリストとして返す。なかった場合は空のリストを返す。
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public override IList<DataRow> GetRelationalRows(DataRow row)
        {
            if(row.Table != ParentTable)
            {
                throw new ArgumentException($"{nameof(ParentTable)}に所属しない列が指定されました。", nameof(row));
            }

            //子テーブルの行のうち、全ての結合条件を満たす行を取得する
            var result =
                ChildTable.Rows.Cast<DataRow>().FirstOrDefault(childRow =>
                {
                    //結合用の列をすべてチェックして、値が一致しない列が無ければ対象となる
                    var isEqual =
                        !joinCols.Any(t => !t.IsEqualRow(row, childRow));
                    return isEqual;
                });

            if (result != null)
            {
                return new List<DataRow>() { result };
            }
            else
            {
                return new List<DataRow>();
            }
        }
    }

    /// <summary>
    /// 1:N結合を表す結合条件
    /// </summary>
    public sealed class TableRelationArray : TableRelation
    {
        public TableRelationArray(DataTable parentTable, DataTable childTable, string jsonPropertyName) : base(parentTable, childTable, jsonPropertyName)
        {
        }

        public override ReturnTypes ReturnType => ReturnTypes.Array;

        public override IList<DataRow> GetRelationalRows(DataRow row)
        {
            if (row.Table != ParentTable)
            {
                throw new ArgumentException($"{nameof(ParentTable)}に所属しない列が指定されました。", nameof(row));
            }

            //子テーブルの行のうち、全ての結合条件を満たす行を取得する
            var result =
                ChildTable.Rows.Cast<DataRow>().Where(childRow =>
                {
                    //結合用の列をすべてチェックして、値が一致しない列が無ければ対象となる
                    var isEqual =
                        !joinCols.Any(t => !t.IsEqualRow(row, childRow));
                    return isEqual;
                })
                .ToList();

            return result;
        }
    }
}
