using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using cs_monkey.Types;

namespace cs_monkey
{
    class Program
    {        
        static AutoResetEvent doneEvent;        

        static int Main(string[] args)
        {            
            Options options = null;
            bool wasSuccessful = false;

            if (args.Length == 0)
            {
                // show help text by default
                args = new string[] { "--help" };
            }

            var result = Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => options = opts);                

            if (result.Tag == ParserResultType.NotParsed)
            {                
#if DEBUG
                Console.ReadLine();
#endif
                return -1;
            }

            try
            {

                doneEvent = new AutoResetEvent(false);

                if (options != null)
                {
                    if (!String.IsNullOrEmpty(options.ConfigFile))
                    {
                        // load configuration from file
                        YamlConfigFile reader = new YamlConfigFile();
                        reader.ReadOptionsFromFile(options.ConfigFile, options);
                    }

                    options.Expand();
                    options.Validate();

                    Console.WriteLine($"Initializing cs-monkey");
                    using (CryptShareMonkeyChrome monkey = new CryptShareMonkeyChrome(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/sender_cache/" + options.SenderEmail))
                    {
                        var aw = RunAsync(monkey, options).ConfigureAwait(false);
                        doneEvent.WaitOne();

                        wasSuccessful = aw.GetAwaiter().GetResult();
                    }                    
                }

                return wasSuccessful ? 0 : -1;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
#if DEBUG
                Console.ReadLine();
#endif
                // return success on error if the upload was successful
                // since this is the actual purpose of this application 
                // (sometimes CefSharp throws an assertion on deinit)
                return wasSuccessful ? 0 : -1;
            }
        }

        private static async Task<bool> RunAsync(ICryptShareMonkey monkey, Options opt)
        {
            // run in separate thread
            return await Task<bool>.Run(async () =>
           {
               bool uploadWasSuccessful = false;

               try
               {
                   if (opt.DemoMode)
                       Console.WriteLine($"Running in DEMO MODE file transfer will only be simulated");

                   Console.WriteLine($"Sending as {opt.SenderName} ({opt.SenderEmail})");
                   Console.WriteLine("Recipients: " + String.Join(", ", opt.Recipients));
                   Console.WriteLine($"{opt.Files.Count()} files: {String.Join(", ", opt.Files.Select(x => System.IO.Path.GetFileName(x)))}");

                   while (!monkey.IsInitialized)
                   {
                       await Task.Delay(100);
                   }

                   var sender = new Sender(opt.SenderName, opt.SenderPhone, opt.SenderEmail);

                   await ExecuteStep("Connecting to cryptshare", () => monkey.OpenConnection(new Uri(opt.Server, UriKind.Absolute), sender));

                   VerificatonState vstate = VerificatonState.Error;

                   await ExecuteStep("Logging in to 'Provide files' mode", async () => { vstate = await monkey.SelectUpload(); return vstate != VerificatonState.Error; });

                   if (vstate == VerificatonState.SenderVerificationRequired)
                   {
                       if (opt.AutoVerificationConfig == null)
                       {
                           Console.WriteLine("Sender verification is needed but no automatic verification config has been provided");
                           throw new Exception("");
                       }

                       await ExecuteStep("Verifying sender", () => monkey.VerifySender(sender, new MailVerificationCodeProvider(opt.AutoVerificationConfig)));
                   }

                   await ExecuteStep("Setting recipients", () => monkey.SetReceivers(opt.Recipients.Select(x => new Receiver(x)).ToArray()));

                   await ExecuteStep("Announcing files", () => monkey.SelectFilesToUpload(opt.Files.ToArray()));

                   PasswordGenerator gen = new PasswordGenerator();
                   string pwd = gen.Generate(12, true);

                   await ExecuteStep("Setting transfer password", () => monkey.SetTransferPassword(pwd));
                   Console.WriteLine($"Transfer password is {pwd}");

                   if (!String.IsNullOrEmpty(opt.PasswordTargetFile))
                   {
                       await WriteFileAsync(opt.PasswordTargetFile, pwd);
                   }

                   await ExecuteStep("Uploading files", () => monkey.DoTransfer(opt.DemoMode, (p) => { Console.WriteLine($"{p:00}%..."); }));
                   uploadWasSuccessful = true;
                   Console.WriteLine("Upload completed successfully");
               }
               catch (Exception e)
               {
                   Console.WriteLine("ERROR: " + e.Message);
               }
               finally
               {
                   doneEvent.Set();                   
               }

               return uploadWasSuccessful;
           });
        }

        private static async Task ExecuteStep(string name, Func<Task<bool>> step)
        {
            Console.Write(name + "...");            

            if (await step())
            {
                Console.WriteLine("OK");                
            }
            else
            {
                Console.WriteLine("FAILED");
                throw new Exception("Step failed");
            }
        }

        private async static Task WriteFileAsync(string filename, string content)
        {
            using (var writer = new StreamWriter(filename, false))
            {
                await writer.WriteAsync(content);
            }
        }
    }
}
