using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey.Types
{
    /// <summary>
    /// A user which has been authorized by the cryptshare server to upload files
    /// </summary>
    [Obsolete]
    public class AuthorizedSender : Sender
    {        
        public static AuthorizedSender None = new AuthorizedSender("", "", "", "", "", "");

        public string CS_UI_Session { get; }

        public string ClientId { get; }        

        public string VerificationToken { get; }

        public AuthorizedSender(string clientId, string name, string phone, string email, string verificationToken, string cs_ui_session)
            : base(name, phone, email)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));            
            VerificationToken = verificationToken ?? throw new ArgumentNullException(nameof(verificationToken));
            CS_UI_Session = cs_ui_session ?? throw new ArgumentNullException(nameof(cs_ui_session));
        }

    }
}
