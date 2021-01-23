using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using SaintCoinach.Ex.Relational.Definition;
using SaintCoinach.Xiv;

using Tharga.Toolkit.Console;
using Tharga.Toolkit.Console.Command;
using Tharga.Toolkit.Console.Command.Base;

#pragma warning disable CS1998

namespace SaintCoinach.Cmd.Commands {
    class JsonCommand : ActionCommandBase {
        private ARealmReversed _Realm;

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

                    foreach (XivRow row in sheet) {
                        var record = new JObject();

                        if (row[0].ToString() == "0") {
                            continue;
                        }

                        for (int i = 0; i < sheet.Header.Columns.Count(); i++) {
                            var col = sheet.Header.Columns.ElementAt(i);

                            if (col.Name != null) {
                                var columnName = CleanColumnName(col.Name);

                                if (col.ValueType == "Item" && sheet.Header.Columns.ElementAt(i + 1).Name.ToLower().StartsWith("amount")) {
                                    var amountCol = sheet.Header.Columns.ElementAt(++i);

                                    if (row[col.Index] != null) {
                                        var itemName = row[col.Index].ToString();
                                        var itemAmount = row[amountCol.Index].ToString();

                                        if (string.IsNullOrWhiteSpace(itemName) && (string.IsNullOrWhiteSpace(itemAmount) || itemAmount == "0")) {
                                            continue;
                                        }

                                        var item = new JObject {
                                                { "item", itemName },
                                                { "amount",  itemAmount}
                                            };

                                        if (col.Definition.InnerDefinition.GetType() == typeof(RepeatDataDefinition) && ((RepeatDataDefinition)col.Definition.InnerDefinition).RepeatCount > 1) {
                                            // First try to get the existing item
                                            JArray itemArray;
                                            if (record.ContainsKey(columnName)) {
                                                itemArray = (JArray)record[columnName];
                                            }
                                            else {
                                                itemArray = new JArray();
                                                record.Add(columnName, itemArray);
                                            }

                                            itemArray.Add(item);
                                        }
                                        else {
                                            record.Add(columnName, item);
                                        }
                                    }
                                }
                                else if(row[col.Index] != null) {
                                    record.Add(columnName, row[col.Index].ToString());
                                }

                            }
                        }
                        output.Add(record);
                    }

                    var formatting = Newtonsoft.Json.Formatting.None;
#if DEBUG
                    //formatting = Newtonsoft.Json.Formatting.Indented;
#endif

                    File.WriteAllText(target.FullName, Newtonsoft.Json.JsonConvert.SerializeObject(output, formatting));


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
            if (columnName.Contains("{")) columnName = columnName.Replace("{", "");
            if (columnName.Contains("}")) columnName = columnName.Substring(0, columnName.IndexOf("}"));

            return columnName;
        }
    }
}
