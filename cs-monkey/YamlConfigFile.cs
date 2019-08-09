using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace cs_monkey
{
    class YamlConfigFile
    {
        public void ReadOptionsFromFile(string filename, Options target)
        {
            using (StreamReader reader = new StreamReader(filename))
            {                
                var deserializer = new Deserializer();
                dynamic config = deserializer.Deserialize(reader);
                dynamic root = config["csmonkey"];

                // replace options which are not yet present in options

                if (String.IsNullOrEmpty(target.Server) && HasProperty(root, "server")) target.Server = root["server"];

                if (HasProperty(root, "sender"))
                {
                    dynamic sender = root["sender"];
                    if (String.IsNullOrEmpty(target.SenderName) && HasProperty(sender, "name")) target.SenderName = sender["name"];
                    if (String.IsNullOrEmpty(target.SenderPhone) && HasProperty(sender, "phone")) target.SenderPhone = sender["phone"];
                    if (String.IsNullOrEmpty(target.SenderEmail) && HasProperty(sender, "email")) target.SenderEmail = sender["email"];                    
                }

                if (HasProperty(root, "verification"))
                {
                    dynamic verify = root["verification"];
                    try
                    {
                        target.AutoVerificationConfig = new Types.ImapConfig(verify["host"], int.Parse(verify["port"]), verify["username"], verify["password"], verify["fromAddress"]);

                        if (HasProperty(verify, "imapFolderPath"))
                            target.AutoVerificationConfig.ImapFolderPath = verify["imapFolderPath"];

                        if (HasProperty(verify, "mailSubject"))
                            target.AutoVerificationConfig.VerificationMailSubject = verify["mailSubject"];
                    } catch (Exception e)
                    {
                        throw new ArgumentException("Section 'verification' in config file is invalid");
                    }
                }

                if (target.Recipients.Count() == 0 && HasProperty(root, "recipients"))
                {
                    target.Recipients = ReadList(root["recipients"]);
                }

                if (target.Files.Count() == 0 && HasProperty(root, "files"))
                {
                    target.Files = ReadList(root["files"]);
                }
            }            
        }

        private List<String> ReadList(dynamic yamlListNode)
        {            
            List<String> lst = new List<string>();
            for (int i = 0; i < yamlListNode.Count; i++)
                lst.Add(yamlListNode[i]);
            return lst;
        }

        private static bool HasProperty(dynamic obj, string name)
        {
            if (obj == null) return false;
            if (obj is IDictionary<object, object> dict)
            {
                return dict.ContainsKey(name);
            }
            return obj.GetType().GetProperty(name) != null;
        }
    }
}
