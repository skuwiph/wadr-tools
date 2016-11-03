namespace adr
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args.Length == 1 && args[0].Equals(commands[HELP_COMMAND], StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException();
                }


                switch (args[0].ToLower())
                {
                    case "init":
                        AdrInit(args);
                        break;
                    case "new":
                        AdrNew(args);
                        break;
                    case "list":
                        ListAdrs();
                        break;
                    case "help":
                        ShowUsage();
                        break;
                    default:
                        Console.WriteLine($"ERROR: unknown command {args[0]}. \r\n\r\nTry adr help for a list of valid commands.");
                        break;
                }
            }
            catch (ArgumentException)
            {
                ShowUsage();
                return 0;
            }
            catch (Exception ex)
            {
                // Ugh
                Console.WriteLine($"{ex.Message}");
                return -1;
            }

            return 0;
        }



        public static void AdrInit(string[] args)
        {
            if (args.Length < 2)
                throw new ApplicationException("ERROR: adr init requires the parameter 'path-to-documentation'");

            string folder = args[1];

            // TODO(ian): check for other args -- perhaps to set an editor specific to this
            // project

            // Write settings in this folder
            AdrSettings settings = new AdrSettings() { Path = folder };
            settings.Write();

            // Write first ADR entry
            AdrEntry.Initialise(settings);

            Console.WriteLine("Adr initialised in current directory\r\n");
            Console.WriteLine("NOTE: Please ensure you have a registered editor in Windows for Markdown (.md) files!");
        }

        public static void AdrNew(string[] args)
        {
            AdrSettings settings = ReadSettings();

            //// TODO(ian): Check to see whether we have the editor environment setting
            //if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ADR_EDITOR")))
            //{
            //    // TODO(ian): Use override editor
            //}

            string title;
            bool deprecate = false;
            int supercedeEntry = -1;
            int titleStarts = 1;

            // Supercede flag must immmediately follow the 'new' command
            if (args[1].Equals("-s"))
            {
                deprecate = true;

                if (!Int32.TryParse(args[2], out supercedeEntry))
                {
                    throw new ApplicationException($"When attempting to supercede an entry, you must specify which entry to supercede!");
                }

                titleStarts = 3;
            }

            // Find title
            StringBuilder s = new StringBuilder();
            for (int i = titleStarts; i < args.Length; ++i) s.Append($"{args[i]} ");
            s.Length -= 1; // remove trailing ' '
            title = s.ToString();

            AdrEntry entry = new AdrEntry(settings, title);
            AdrEntry deprecateEntry = null;

            if (deprecate)
            {
                deprecateEntry = new adr.AdrEntry(settings, supercedeEntry);
            }

            if (entry.Edit())
            {
                if (deprecateEntry != null)
                    deprecateEntry.DeprecateBy(entry);
            }
        }

        public static void ListAdrs()
        {
            AdrSettings settings = ReadSettings();

            List<string> files = AdrEntry.List(settings);

            Console.WriteLine($"Contents of {settings.Path}:\r\n");
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file).Replace("-", " ");
                Console.WriteLine($"{name}");
            }
        }

        public static void ShowUsage()
        {
            Console.WriteLine("\r\nADR - A command-line tool for working with Architecture Decision Records (ADRs).\r\nBased on adr-tools (https://github.com/npryce/adr-tools).\r\n");
            Console.WriteLine("Usage:\r\n\r\nadr <command> <params>, where <command> is one of:");
            for (int i = 0; i < commands.Length; ++i)
            {
                Console.WriteLine($"\t{commands[i]} \t\t- {commands_help[i]}");
            }
        }

        // Certain functions require the presence of an adr-settings.json file!
        private static AdrSettings ReadSettings()
        {
            if (!File.Exists("adr-settings.json"))
            {
                Console.WriteLine("Expected to find an adr-settings.json file in the current directory. Use adr init in the project home directory to generate this file.");
            }

            AdrSettings settings = new AdrSettings();
            settings.Read();

            return settings;
        }

        private static void WriteStringToFile(string filename, string outputText)
        {
            using (StreamWriter sr = new StreamWriter(filename))
            {
                sr.Write(outputText);
                sr.Close();
            }
        }

        // Hmp, hacky.
        static string[] commands = { "init", "new", "list", "help" };
        static string[] commands_help = { "Initialise adr for this project.", "Add or supercede an adr record.", "List all ADRs", "List available commands" };

        // ugly, must be a better way
        static int HELP_COMMAND = 3;
    }

    public class AdrEntry
    {
        public AdrEntry(AdrSettings settings, string title)
        {
            Title = title;
            Date = DateTime.Now;
            Status = "Proposed";
            Context = "Context here..."; // "This section describes the forces at play, including technological, political, social, and project local. These forces are probably in tension, and should be called out as such. The language in this section is value-neutral. It is simply describing facts.";
            Decision = "We will ..."; // "This section describes our response to these forces. It is stated in full sentences, with active voice. \"We will ...\"";
            Consequences = "Consequences of decision..."; // "This section describes the resulting context, after applying the decision. All consequences should be listed here, not just the \"positive\" ones. A particular decision may have positive, negative, and neutral consequences, but all of them affect the team and project in the future.\r\n\r\nThe whole document should be one or two pages long.We will write each ADR as if it is a conversation with a future developer.This requires good writing style, with full sentences organized into paragraphs. Bullets are acceptable only for visual style, not as an excuse for writing sentence fragments. (Bullets kill people, even PowerPoint bullets.)";

            _settings = settings;
        }

        public AdrEntry(AdrSettings settings)
        {
            Date = DateTime.Now;

            _settings = settings;
        }

        public AdrEntry(AdrSettings settings, int deprecateNumber)
        {
            Number = deprecateNumber;
            _settings = settings;

            List<string> files = List(_settings);
            string supercededFileName = string.Empty;
            foreach (string f in files)
            {
                if (Path.GetFileName(f).StartsWith($"{Number:0000}-"))
                {
                    int start = f.IndexOf('-') + 1;
                    string title = f.Substring(start, f.LastIndexOf('.') - start);
                    Title = title.Replace("-", " ");

                    return;
                }
            }

            throw new ApplicationException($"The adr entry {Number} does not exist to be deprecated!");
        }

        public int Number { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Context { get; set; }
        public string Decision { get; set; }
        public string Consequences { get; set; }

        public string FileName => $"{Number:0000}-{Title.Replace(" ", "-")}.md";

        public bool Edit()
        {
            // Ensure number is correct!
            GetLatestAdrNumber();

            string temp = Path.Combine(Path.GetTempPath(), FileName);
            
            // Must write this first to the temp directory
            Write(Path.GetTempPath());

            DateTime created = File.GetLastWriteTimeUtc(temp);

            ProcessStartInfo pInfo = new ProcessStartInfo();
            pInfo.FileName = temp;
            Process p = Process.Start(pInfo);
            p.WaitForInputIdle();                               // Wait for the window to finish loading.
            p.WaitForExit();                                    // Wait for the process to end.

            DateTime write = File.GetLastWriteTimeUtc(temp);

            if (write > created)
            {
                // Copy temp file to settings.Path location
                File.Move(temp, Path.Combine(_settings.Path, FileName));
            }
            else
            {
                File.Delete(temp);
            }

            return write > created;
        }

        public bool DeprecateBy(AdrEntry newEntry)
        {
            StringBuilder newContents = new StringBuilder();

            string oldFileName = Path.Combine(_settings.Path, FileName);
            string newFileName = Path.Combine(_settings.Path, $"{Path.GetFileNameWithoutExtension(FileName)}-(superceded).md");
            // open file
            using (StreamReader sr = new StreamReader(oldFileName))
            {
                string contents = sr.ReadToEnd();

                int statusStart = contents.IndexOf("## Status");
                int statusEnd = contents.IndexOf("## Context");

                newContents.Append(contents.Substring(0, statusStart + 9));
                newContents.Append($"\r\nSuperceded by {newEntry.FileName}\r\n");
                newContents.Append(contents.Substring(statusEnd));
            }

            // Write file back
            using (StreamWriter sr = new StreamWriter(oldFileName))
            {
                sr.Write(newContents.ToString());
            }

            // Want to put (deprecated) in file name
            File.Move(oldFileName, newFileName);

            return true;
        }

        public void Write(string path)
        {
            using (StreamWriter sr = new StreamWriter(Path.Combine(path, FileName)))
            {
                sr.WriteLine($"# {Number}. {Title}\r\n");
                sr.WriteLine($"Date: {DateTime.Now.ToString("dd/MM/yyyy")}\r\n");
                sr.WriteLine($"## Status\r\n");
                sr.WriteLine($"{Status}\r\n");
                sr.WriteLine($"## Context\r\n");
                sr.WriteLine($"{Context}\r\n");
                sr.WriteLine($"## Decision\r\n");
                sr.WriteLine($"{Decision}\r\n");
                sr.WriteLine($"## Consequences\r\n");
                sr.WriteLine($"{Consequences}\r\n");
                sr.Close();
            }
        }


        private void GetLatestAdrNumber()
        {
            // Find latest adr number from adr-directory
            List<string> files = List(_settings);

            string lastName = Path.GetFileName(files[files.Count - 1]);
            string numberFromFile = lastName.Substring(0, 4);
            int number = 0;
            if (!Int32.TryParse(numberFromFile, out number))
            {
                throw new ApplicationException($"Couldn't get last number from file name {lastName}!");
            }

            Number = number + 1;
        }

        public static void Initialise(AdrSettings settings)
        {
            AdrEntry adr = new AdrEntry(settings);

            adr.Number = 1;
            adr.Title = "Record architectural decisions";
            adr.Status = "Accepted";
            adr.Context = "We need to record the architectural decisions made on this project.";
            adr.Decision = "We will use Architecture Decision Records, as described by Michael Nygard in this article: http://thinkrelevance.com/blog/2011/11/15/documenting-architecture-decisions";
            adr.Consequences = "See Michael Nygard's article, linked above.";

            adr.Write(settings.Path);
        }

        public static List<string> List(AdrSettings settings)
        {
            List<string> files = new List<string>(Directory.GetFiles(settings.Path));
            files.Sort();

            return files;
        }

        private AdrSettings _settings;

    }

    public class AdrSettings
    {
        public string Path { get; set; }

        public void Read()
        {
            using (StreamReader sr = new StreamReader("adr-settings.json"))
            {
                string contents = sr.ReadToEnd();
                string[] split = contents.Split(new[] { "\r\n", "\r", "\n", "\"", "\t", ":" }, StringSplitOptions.RemoveEmptyEntries);
                for(int i = 0; i < split.Length; ++i)
                {
                    if(split[i].Equals("path", StringComparison.OrdinalIgnoreCase))
                    {
                        this.Path = split[i + 1];
                    }
                }
            }

            if (string.IsNullOrEmpty(Path))
                throw new ApplicationException("Expected a 'path' settings in adr-settings.json!");

            // Ensure path exists
            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
            }
        }

        public void Write()
        {
            using (StreamWriter sr = new StreamWriter("adr-settings.json"))
            {
                sr.Write($"{{\r\n\t\"path\":\"{Path}\"\r\n}}");
                sr.Close();
            }

            // Ensure path exists
            if (!Directory.Exists(Path))
            {
                Directory.CreateDirectory(Path);
            }
        }
    }
}
