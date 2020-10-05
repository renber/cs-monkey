using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey.Types
{
    public interface ICryptShareMonkey : IDisposable
    {                
        bool IsInitialized { get; }

        Task<bool> OpenConnection(Uri url, Sender sender);

        Task<VerificatonState> SelectUpload();

        Task<bool> VerifySender(Sender sender, IVerificationCodeProvider codeProvider);

        Task<bool> SetReceivers(params Receiver[] receivers);

        Task<bool> SelectFilesToUpload(params string[] filenames);

        Task<bool> SetTransferPassword(string password);

        Task<bool> DoTransfer(bool dryrun, Action<float> uploadProgressCallback, TimeSpan? uploadTimeout = null);
    }
}
