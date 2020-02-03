using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;

namespace cs_monkey.Types
{
    public class CryptShareMonkeyChrome : ICryptShareMonkey
    {         
        TimeSpan defaultTimeout = TimeSpan.FromMinutes(2);
        TimeSpan intermediateWaitTime = TimeSpan.FromSeconds(2);

        private AsyncAutoResetEvent documentCompletedEvent = new AsyncAutoResetEvent(false);        

        protected IWebBrowser browser;

        Uri startUri;

        TestCookieVisitor cookieVisitor;

        public CryptShareMonkeyChrome(string cacheFolder)
        {
            cookieVisitor = new TestCookieVisitor();

            // automation has been written for the German version of the website, so request this
            CefSettings s = new CefSettings();
            s.AcceptLanguageList = "de-DE";
            if (!String.IsNullOrEmpty(cacheFolder))
                s.CachePath = cacheFolder;
            Cef.Initialize(s);

            browser = CreateBrowser();
            browser.FrameLoadEnd += Browser_FrameLoadEnd;
            browser.LoadingStateChanged += Browser_LoadingStateChanged;
            browser.JsDialogHandler = new JsNoDialogHandler();
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
           //Cef.GetGlobalCookieManager().VisitAllCookies(cookieVisitor);
        }

        protected virtual IWebBrowser CreateBrowser()
        {
            return (IWebBrowser)new CefSharp.OffScreen.ChromiumWebBrowser("");
        }

        public bool IsInitialized => browser.IsBrowserInitialized;

        private void Browser_LoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
        {
            if (!e.IsLoading)
                documentCompletedEvent.Set();
        }

        public async Task<bool> OpenConnection(Uri url, Sender sender)
        {
            startUri = url;
            
            browser.Load(url.AbsoluteUri);            

            await documentCompletedEvent.WaitAsync(defaultTimeout);
            
            return true;
        }

        /// <summary>
        /// Clicks the Upload-Button on the start page
        /// </summary>
        /// <returns></returns>
        public async Task<VerificatonState> SelectUpload()
        {
            // click the provide button
            if (!(await ClickBtn("Bereitstellen")))
                return VerificatonState.Error;

            await documentCompletedEvent.WaitAsync(defaultTimeout);

            await Task.Delay(intermediateWaitTime);

            // check if we landed at the sender verification page
            var verificationCheck = await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:verification:name:textInput').length");
            if ((int)verificationCheck.Result > 0)
            {
                // we have to verify the sender first
                return VerificatonState.SenderVerificationRequired;
            }
            
            if (!await IsOnPage("Empfänger"))
            {
                return VerificatonState.Error;
            }

            return VerificatonState.ReadyToUpload;
        }

        public async Task<bool> VerifySender(Sender sender, IVerificationCodeProvider codeProvider)
        {
            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:verification:name:textInput')[0].value = '{sender.Name}';");
            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:verification:phone:phoneInput')[0].value = '{sender.Phone}';");
            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:verification:mail:emailInput')[0].value = '{sender.Email}';");
            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:verification:termsOfUse:checkbox')[0].checked = 'true';");

            // request the verification code
            if (!(await ClickBtn("Weiter")))
                return false;

            await documentCompletedEvent.WaitAsync(defaultTimeout);
            await Task.Delay(intermediateWaitTime);

            if (!await IsOnPage("Verifizierung der E-Mail-Adresse"))
            {
                return false;
            }

            string code = await codeProvider.GetVerificationCode();
            if (String.IsNullOrEmpty(code))
                return false;

            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('verification:veriCode')[0].value = '{code}';");

            if (!(await ClickBtn("Weiter")))
                return false;

            await documentCompletedEvent.WaitAsync(defaultTimeout);
            await Task.Delay(intermediateWaitTime);

            // check if we landed at the receiver definition page            
            if (!(await IsOnPage("Empfänger")))                
                return false;

            return true;
        }

        public async Task<bool> SetReceivers(params Receiver[] receivers)
        {
            if (receivers.Length == 0)
                new ArgumentException("At least one receiver has to be set");

            // set target emails as comma separated value
            string receiverText = "[" + String.Join(",", receivers.Select(r => "\"" + r.Email + "\"")) + "]";
            await browser.GetMainFrame().EvaluateScriptAsync($"document.getElementsByName('recipients:to:itemList')[0].value = '{receiverText}';");
            
            // click next
            if (! (await ClickBtn("Weiter")))
                return false;

            await documentCompletedEvent.WaitAsync(defaultTimeout);
            await Task.Delay(intermediateWaitTime);

            if (! (await IsOnPage("Dateien hinzufügen")))                
                return false;
            
            return true;
        }

        public async Task<bool> SelectFilesToUpload(params string[] filenames)
        {
            // suppress file dialog -> return filenames directly
            browser.DialogHandler = new FileUploadDialogHandler(filenames);

            // find the browse files button's location
            // due to security constraints we can not invoke a file dialog by using javasript
            // so simulate a click on the button
            var tsk = await browser.GetMainFrame().EvaluateScriptAsync("document.getElementsByName('html5UploadInput')[0].getBoundingClientRect();");
            dynamic rect = tsk.Result;
            int x = (int)rect.left;
            int y = (int)rect.top;
            int w = (int)rect.right - x;
            int h = (int)rect.bottom - y;

            browser.GetBrowser().GetHost().SendMouseClickEvent(x + w/2, y + h/2, MouseButtonType.Left, false, 1, CefEventFlags.None);
            await Task.Delay(15);
            browser.GetBrowser().GetHost().SendMouseClickEvent(x + w/2, y + h/2, MouseButtonType.Left, true, 1, CefEventFlags.None);

            await Task.Delay(intermediateWaitTime);

            // next step
            if (! (await ClickBtn("Weiter")))
                return false;
            
            // does not navigate to a new url but replaces current site content
            await Task.Delay(intermediateWaitTime);
            
            if (! (await IsOnPage("Sicherheitseinstellungen")))
                return false;

            return true;
        }

        public async Task<bool> SetTransferPassword(string password)
        {
            // set password two both password fiels (pwd & repeated pwd field)
            await browser.GetMainFrame().EvaluateScriptAsync($"Array.prototype.slice.call(document.querySelectorAll('input[type=password]')).forEach(x => x.value = '{password}');");

            if (! (await ClickBtn("Weiter")))
               return false;

            // does not navigate to a new url but replaces current site content
            await Task.Delay(intermediateWaitTime);
            
            if (!(await IsOnPage("Empfängerbenachrichtigung")))
                return false;

            return true;
        }

        public async Task<bool> DoTransfer(bool dryrun, Action<float> uploadProgressCallback, TimeSpan? uploadTimeout = null)
        {
            if (dryrun)
            {
                // simulate the transfer process using a local page
                string basepath = AppDomain.CurrentDomain.BaseDirectory;
                string staticMockUploadSite = basepath + "/staticweb/mock/Befine Cryptshare Uploading.html";
                staticMockUploadSite = System.IO.Path.GetFullPath(staticMockUploadSite);
                browser.Load(staticMockUploadSite);                

            } else
            {
                if (!(await ClickBtn("Transfer starten")))
                    return false;
            }

            // monitor upload progress and success message

            float uploadProgress = 0;
            long started = Environment.TickCount;
            TimeSpan timeout = uploadTimeout ?? TimeSpan.FromMinutes(5);
            bool timeoutTriggered = false;
            bool uploadSuccessful = false;

            while (true)
            {                
                if (uploadProgress < 100)
                {                    
                    var t = await browser.GetMainFrame().EvaluateScriptAsync(@"
pbars = document.getElementsByClassName('progress-bar-success');
if (pbars.length > 0)
  pbars[0].style.width;
else
  'na';");
                    if (t.Success)
                    {
                        String pText = t.Result?.ToString() ?? "na";
                        if (float.TryParse(pText.TrimEnd('%'), out float newProgress) && uploadProgress != newProgress)
                        {
                            uploadProgress = newProgress;
                            uploadProgressCallback?.Invoke(uploadProgress);
                        }
                    }                    
                }

                if (!uploadSuccessful)
                {
                    // check for the success message
                    var t = await browser.GetMainFrame().EvaluateScriptAsync(@"
msgs = document.getElementsByClassName('alert-success summary');
if (msgs.length > 0)
    msgs[0].innerText;
else
    '';");
                    {
                        if (t.Success)
                        {
                            String alertText = t.Result?.ToString() ?? "";
                            // todo: english translation
                            if (alertText.ToLower().Contains("die dateien wurden erfolgreich hochgeladen und verschlüsselt"))
                            {
                                uploadProgressCallback?.Invoke(100);
                                uploadSuccessful = true;
                                break;
                            }
                        }
                    }
                }
                
                // check timeout
                if (Environment.TickCount - started >= timeout.TotalMilliseconds)
                {
                    timeoutTriggered = true;
                    break;
                }

                await Task.Delay(500);
            }

            return !timeoutTriggered && uploadSuccessful;            
        }        

        private async Task<bool> ClickBtn(string text)
        {

            await browser.GetMainFrame().EvaluateScriptAsync($@"
            btns = Array.prototype.slice.call(document.getElementsByClassName('btn'));            
            btns.find(x => x.innerText.trim() === '{text}').click();");

            return true;
        }

        /// <summary>
        /// checks if a header element on the current page has the given title
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        private async Task<bool> IsOnPage(string title)
        {
            var tsk = await browser.GetMainFrame().EvaluateScriptAsync($"Array.prototype.slice.call(document.getElementsByTagName('h3')).some(x => x.innerText.trim() === '{title}');");
            return tsk.Result is bool b && b;
        }
    }

    class FileUploadDialogHandler : IDialogHandler
    {
        string[] filenames;
        
        public FileUploadDialogHandler(params string[] filenames)
        {
            this.filenames = filenames;
        }

        public bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, CefFileDialogFlags flags, string title, string defaultFilePath, List<string> acceptFilters, int selectedAcceptFilter, IFileDialogCallback callback)
        {
            if (mode == CefFileDialogMode.OpenMultiple)
            {
                callback.Continue(0, filenames.ToList());
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Suppress all javascript dialogs
    /// </summary>
    class JsNoDialogHandler : IJsDialogHandler
    {
        public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            callback.Continue(true);
            return true;
        }

        public void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            
        }

        public bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            callback.Continue(true);
            return true;
        }

        public void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            
        }
    }

    class TestCookieVisitor : ICookieVisitor
    {
        public string Session { get; private set; }

        public void Dispose()
        {
            
        }

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            if (cookie.Name == "cs-ui-session")
                Session = cookie.Value;

            return true;
        }
    }

    public enum VerificatonState
    {
        Error,
        SenderVerificationRequired,
        ReadyToUpload
    }
}
