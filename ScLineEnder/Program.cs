using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using log4net;

namespace ScLineEnder 
{
    class Program 
    {
        private static readonly ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        static void Main(string[] args) 
        {
            var sFolders = ConfigurationManager.AppSettings["StartFolder"].Split(';').ToList();

            foreach (var sFolder in sFolders)
            {
                Logger.InfoFormat("StartFolder is: {0}", sFolder);

                if (!Directory.Exists(sFolder))
                {
                    Logger.Info("StartFolder is: not exist");
                    return;
                }
                DirectorySearch(sFolder);
            } 
        }

        static void DirectorySearch(string directory)
        {
            try
            {
                foreach (var f in Directory.GetFiles(directory, "*.item"))
                    {
                        if (!File.Exists(f))
                            continue;
                        Logger.InfoFormat("file - {0}-{1}", directory, f);
                        Console.WriteLine(f);

                        ProcessFile(f);
                    }

                foreach (var d in Directory.GetDirectories(directory))
                {
                    if (!Directory.Exists(d))
                        continue;
                    Logger.InfoFormat("directory - {0}", d);

                    foreach (var f in Directory.GetFiles(d, "*.item"))
                    {
                        if (!File.Exists(f))
                            continue;
                        Logger.InfoFormat("file - {0}-{1}", d, f);
                        Console.WriteLine(f);

                        ProcessFile(f);
                    }
                    DirectorySearch(d);
                }
            }
            catch (Exception excpt)
            {
                Logger.Error(excpt);
                Console.WriteLine(excpt.Message);
            }
        }

        static void ProcessFile(string filePath)
        {
            var rootItem = new ScFile();

            using (var reader = new StreamReader(filePath))
            //using (var writer = new StreamWriter (filePath))
            {
                //string text = reader.ReadLine();
                var item = new ScBaseField();
                var ready = false;

                while (!ready)
                {
                    string text = reader.ReadLine();
                    switch (text)
                    {
                        case ItemHeader:
                            break;
                        case VersionHeader:
                            rootItem.Fields.Add(item);
                            item = new ScVersion();
                            break;
                        case FieldHeader:
                            rootItem.Fields.Add(item);
                            item = new ScField();
                            break;
                        case null:
                            rootItem.Fields.Add(item);
                            ready = true;
                            break;
                        default: item.Values.Add(text);
                            break;
                    }
                }
            }

            //var np = filePath.Replace(@"C:\temp\test\tds", @"C:\temp\test1\tds");

            using (var writer = new StreamWriter(filePath))
            {
                foreach (var field in rootItem.Fields)
                {
                    writer.Write(field.GetText());
                }
                
            }
        }

        public const string VersionHeader = "----version----";
        public const string FieldHeader = "----field----";
        public const string ItemHeader = "----item----";
        public const string ContentLength = "content-length: ";
        public const string BlobItem1 = "name: Blob";
        public const string BlobItem2 = "key: blob";

        public class ScFile
        {
            public List<ScBaseField> Fields = new List<ScBaseField>();
        }

        public class ScBaseField
        {
            public List<string> Values = new List<string>();

            public List<string> GetValues(StringReader sr)
            {
                var list = new List<string>();
                while (true)
                {
                    var line = sr.ReadLine();
                    if (string.IsNullOrEmpty(line))
                    {
                        break;
                    }
                    if (line.Equals(VersionHeader, StringComparison.InvariantCultureIgnoreCase) ||
                        line.Equals(FieldHeader, StringComparison.InvariantCultureIgnoreCase) ||
                        line.Equals(ItemHeader, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return list;
                    }
                    else
                    {
                        list.Add(line);
                    }
                }
                return list;
            }

            public virtual string GetText()
            {
                var sb = new StringBuilder();
                sb.AppendLine(ItemHeader);

                foreach (string value in Values)
                {
                    sb.AppendLine(value);
                }

                //sb.AppendLine();

                return sb.ToString();
            }
        }

        public class ScVersion : ScBaseField
        {
            public override string GetText()
            {
                var sb = new StringBuilder();
                sb.AppendLine(VersionHeader);

                foreach (string value in Values)
                {
                    sb.AppendLine(value);
                }

                //sb.AppendLine();

                return sb.ToString();
            }
        }

        public class ScField : ScBaseField
        {
            private string _value = string.Empty;
            public override string GetText()
            {
                var edited = ModifyResult();

                var sb = new StringBuilder();
                sb.AppendLine(FieldHeader);

                foreach (string value in Values)
                {
                    sb.AppendLine(value);
                }

                if (edited)
                {
                    sb.AppendLine();
                    sb.AppendLine(_value);
                }

                return sb.ToString();
            }



            public bool ModifyResult()
            {
                if (Values.IndexOf(BlobItem1) > 0 || Values.IndexOf(BlobItem2) > 0)
                    return false;

                var updatedValues = new List<string>();

                var clItemIndex = Values.FindIndex(x => x.StartsWith(ContentLength, StringComparison.InvariantCultureIgnoreCase));

                var clItem = Values[clItemIndex];

                var valueItems = Values.Skip(clItemIndex + 1).Take(Values.Count - clItemIndex + 1);

                var sb = new StringBuilder();

                foreach (var item in valueItems)
                {
                    sb.Append(item);
                }

                _value = sb.ToString();

                Values[clItemIndex] = string.Format("{0}{1}", ContentLength, _value.Length);

                updatedValues.AddRange(Values.Take(clItemIndex + 1));

                Values = updatedValues;

                return true;
            }
        }
    }
}
