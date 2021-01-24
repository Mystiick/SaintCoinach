using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using SaintCoinach.Ex.Relational;
using SaintCoinach.Ex.Relational.Definition;
using SaintCoinach.Xiv;

using Tharga.Toolkit.Console;
using Tharga.Toolkit.Console.Command;
using Tharga.Toolkit.Console.Command.Base;

#pragma warning disable CS1998

namespace SaintCoinach.Cmd.Commands {
    class JsonCommand : ActionCommandBase {
        private ARealmReversed _Realm;
        private Queue<RelationalColumn> _Columns;

        public JsonCommand(ARealmReversed realm)
            : base("json", "Export all data (default), or only specific data files, in a JSON format.") {
            _Realm = realm;
        }

        public override async Task<bool> InvokeAsync(string paramList) {
            const string JsonFileFormat = "json/{0}.json";

            IEnumerable<string> filesToExport;

            if (string.IsNullOrWhiteSpace(paramList))
                filesToExport = _Realm.GameData.AvailableSheets;
            else
                filesToExport = paramList.Split(' ').Select(_ => _Realm.GameData.FixName(_));

            var successCount = 0;
            var failCount = 0;
            foreach (var name in filesToExport) {
                var target = new FileInfo(Path.Combine(_Realm.GameVersion, string.Format(JsonFileFormat, name)));
                try {
                    var sheet = _Realm.GameData.GetSheet(name);

                    if (!target.Directory.Exists)
                        target.Directory.Create();

                    var output = new List<JObject>();

                    if (sheet is XivSheet2<XivSubRow> s2) {
                        var record = new JObject();
                        foreach (var row in s2) {
                            record.Add(row.FullKey, row.DefaultValue?.ToString());
                        }
                        output.Add(record);
                    }
                    else {
                        foreach (var row in sheet) {
                            var record = new JObject();

                            if (sheet is IXivSubRow || row[0] == null || row[0].ToString() == "0") {
                                continue;
                            }

                            _Columns = new Queue<RelationalColumn>(sheet.Header.Columns);
                            while (_Columns.Count > 0) {
                                var column = _Columns.Dequeue();
                                var element = ProcessSingleColumn(column, row);

                                if (element != null) {
                                    AddItemToRecord(element, column, ref record);
                                }
                            }
                            output.Add(record);
                        }
                    }

                    var settings = new Newtonsoft.Json.JsonSerializerSettings() {
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                    };

                    File.WriteAllText(target.FullName, Newtonsoft.Json.JsonConvert.SerializeObject(output, settings));

                    ++successCount;
                }
                catch (Exception e) {
                    OutputError("Export of {0} failed: {1}", name, e.Message);
                    try { if (target.Exists) { target.Delete(); } } catch { }
                    ++failCount;
                }
            }
            OutputInformation("{0} files exported, {1} failed", successCount, failCount);

            return true;
        }

        private string CleanColumnName(string columnName) {
            // Remove { } from the name
            if (columnName.Contains("{")) columnName = columnName.Replace("{", "").Replace("}", "");
            
            // Remove any [0], [1]... from the name. eg:   "BossExp[0]" -> "BossExp"
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("\\[(\\d+\\-?)+]");
            var match = regex.Match(columnName);

            while (match.Success) {
                columnName = columnName.Replace(match.Value, "");
                match = regex.Match(columnName);
            }
           
            return columnName;
        }

        private object ProcessSingleColumn(RelationalColumn column, XivRow row) {
            if (column.Name != null) {
                if (column.ValueType == "Item" && _Columns.Count > 0 && (_Columns.Peek()?.Name?.ToLower()?.StartsWith("amount")).GetValueOrDefault()) {
                    var amountCol = _Columns.Dequeue();

                    if (row[column.Index] != null) {
                        var itemName = row[column.Index].ToString();
                        var itemAmount = row[amountCol.Index].ToString();

                        if (string.IsNullOrWhiteSpace(itemName) && (string.IsNullOrWhiteSpace(itemAmount) || itemAmount == "0")) {
                            return null;
                        }

                        return new JObject {
                            { "item", itemName },
                            { "amount",  itemAmount}
                        };
                    }
                }
                else if (row[column.Index] != null) {
                    return row[column.Index].ToString();
                }
            }
            return null;
        }

        private void AddItemToRecord(object element, RelationalColumn column, ref JObject record) {
            var columnName = CleanColumnName(column.Name);

            // Check if it's a repeating item. If it is, look for an existing element with that name. If one cannot be found, create a new array and add it to the object
            // If it is not a repeating item, add it as is to the record
            if (column.Definition.InnerDefinition is RepeatDataDefinition def && def.RepeatCount > 1) {
                // First try to get the existing item
                JArray itemArray;
                if (record.ContainsKey(columnName)) {
                    itemArray = (JArray)record[columnName];
                }
                else {
                    itemArray = new JArray();
                    record.Add(columnName, itemArray);
                }

                itemArray.Add(element);
            }
            else if (record.ContainsKey(columnName)) {
                // This is a duplicate named column (perhaps from cleaning the string)
                // Convert it to an array if it hasn't been yet, and add the new record to it
                JToken item = record[columnName];
                JArray itemArray;

                if (item is JArray ja) {
                    ja.Add(item);
                    itemArray = (JArray)record[columnName];
                }
                else {
                    // Convert the Item to an array
                    itemArray = new JArray() { item };
                    record[columnName] = itemArray;
                }

                itemArray.Add(element);
            }
            else {

                if (element is JObject jobj) {
                    record.Add(columnName, jobj);
                }
                else {
                    record.Add(columnName, element.ToString());
                }

            }
        }
    }
}
