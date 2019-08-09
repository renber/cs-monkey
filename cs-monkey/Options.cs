using CommandLine;
using cs_monkey.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey
{
    class Options
    {

        [Option('s', "server", Required = false, HelpText = "Cryptshare server address")]
        public string Server { get; set; }        
        
        public string SenderName { get; set; }
        
        public string SenderEmail { get; set; }
        
        public string SenderPhone { get; set; }                

        [Option('o', "opwd", Required = false, HelpText = "Writes the used transfer password to the specified file")]
        public string PasswordTargetFile { get; set; }

        [Option('c', "config-file", Required = false, HelpText = "Configuration file to read sender and verification options from")]
        public string ConfigFile { get; set; }

        [Option('r', "recipients", Required = false, HelpText = "Email address of recipients")]
        public IEnumerable<string> Recipients { get; set; }

        [Option('f', "files", Required = false, HelpText = "File paths of files to upload")]
        public IEnumerable<string> Files { get; set; }

        [Option("demo", Required = false, Default = false, HelpText = "If enabled, transfer is prepared but not executed (runs a transfer simulation for the final step).")]
        public bool DemoMode { get; set; }

        public ImapConfig AutoVerificationConfig { get; set; } = null;

        /// <summary>
        /// Expands file globs in the files list if any
        /// </summary>
        public void Expand()
        {
            if (Files != null && Files.Any(x => x.Contains("*")))
            {
                List<String> expandedFiles = new List<string>();
                foreach(var f in Files)
                {
                    if (f.Contains("*"))
                    {
                        var foundFiles = System.IO.Directory.GetFiles(System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(f)), System.IO.Path.GetFileName(f), System.IO.SearchOption.TopDirectoryOnly);
                        expandedFiles.AddRange(foundFiles);
                    }
                    else
                        expandedFiles.Add(System.IO.Path.GetFullPath(f));
                }

                Files = expandedFiles;
            }
        }

        public void Validate()
        {
            if (String.IsNullOrEmpty(SenderName)) throw new Exception("Sender name missing");
            if (String.IsNullOrEmpty(SenderEmail)) throw new Exception("Sender email missing");
            if (String.IsNullOrEmpty(SenderPhone)) throw new Exception("Sender phone missing");            
            if (String.IsNullOrEmpty(Server)) throw new Exception("Server address missing");            
            if (Recipients.Count() == 0) throw new Exception("No recipients have been defined");
            if (Files.Count() == 0) throw new Exception("No files have been defined");

            List<String> missingFiles = new List<string>();
            foreach(var f in Files)
            {
                if (!System.IO.File.Exists(f))
                {
                    missingFiles.Add(f);
                }
            }

            if (missingFiles.Count > 0)
                throw new Exception("The following files selected for upload could not be found: " + String.Join(", ", missingFiles));
        }
    }
}
