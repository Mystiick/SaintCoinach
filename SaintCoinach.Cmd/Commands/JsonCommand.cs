using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

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

                    var jo = new List<JObject>();

                    foreach (var sh in sheet) {
                        var jo2 = new JObject();
                        
                        foreach (var col in sheet.Header.Columns) {
                            //Console.WriteLine($"[{col.Index}] {col}: {sh.GetRaw(col.Index)}");
                            if (col.Name != null && sh[col.Index] != null) {
                                jo2.Add(col.Name, sh[col.Index].ToString());
                            }
                        }
                        jo.Add(jo2);
                    }

#if DEBUG
                    var formatting = Newtonsoft.Json.Formatting.Indented;
#else
                    var formatting = Newtonsoft.Json.Formatting.None;
#endif

                    File.WriteAllText(target.FullName, Newtonsoft.Json.JsonConvert.SerializeObject(jo, formatting));


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
    }
}
